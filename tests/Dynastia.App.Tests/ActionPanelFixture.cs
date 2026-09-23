using Dynastia.App.ViewModels;
using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;

namespace Dynastia.App.Tests;

/// <summary>Exercises observable view-model APIs without starting Avalonia or loading saves.</summary>
internal sealed class ActionPanelFixture : IDisposable
{
    private int _nextId;
    public ActionPanelFixture(IFarmingService? farmingService = null)
    {
        Head = Person(30, "Actor");
        var residence = new TownInfo("Z Residence", "County", 20, 52, 10000)
        { Id = "residence", RegionId = "region", PolityName = "Polity" };
        Locations = new LocationsStub(residence);
        Economy = new EconomyStub(Head, residence);
        Succession = new SuccessionStub(Head);
        Actions = new ActionRegistry(State, Events, Random, new ActionGuardRegistry());
        Registry = new RecordingActionRegistry(Actions);
        // Lifecycle/start/save dependencies are unused by these presentation tests.
        View = new MainWindowViewModel(
            gameState: State,
            newGameService: null!,
            yearProcessor: null!,
            selectionService: Selection,
            statsService: null,
            familyService: null,
            nationalityService: null,
            healthService: null,
            stressService: null,
            economyService: Economy,
            houseMarketService: null,
            townProsperityService: null,
            farmingService: farmingService,
            craftService: null,
            loanService: null,
            heirloomService: null,
            statusService: null,
            householdService: null,
            autonomousHouseholdDecisionService: null,
            adoptionService: null,
            locationService: Locations,
            localCareerOpportunityService: new Opportunities(Locations),
            marriageSatisfactionService: null,
            familyRelationService: null,
            householdConnectionService: null,
            thoughtService: null,
            hobbyService: null,
            personalityService: null,
            appearanceService: null,
            childHappinessService: null,
            educationService: null,
            careerService: null,
            careerPresentationService: null,
            partnerSearchService: null,
            justiceService: null,
            biographyService: null,
            historicalEventService: null,
            succession: Succession,
            eventBus: Events,
            actionRegistry: Registry,
            saveService: null!,
            reconciliation: null!);
    }

    public GameState State { get; } = new() { Year = 1900 };
    public GameEventBus Events { get; } = new();
    public SelectionService Selection { get; } = new();
    public SequenceGameRandom Random { get; } = new();
    public IPerson Head { get; }
    public ActionRegistry Actions { get; }
    public RecordingActionRegistry Registry { get; }
    public MainWindowViewModel View { get; }
    public EconomyStub Economy { get; }
    public LocationsStub Locations { get; }
    public SuccessionStub Succession { get; }

    public IPerson Person(int age, string name)
    {
        var p = State.CreatePerson(name, "Test", age, Guid.Parse($"00000000-0000-0000-0000-{++_nextId:D12}"));
        p.Tags.Add("state.alive");
        return p;
    }

    public void Select(IPerson? person = null)
    {
        var p = person ?? Head;
        var row = new PersonRowViewModel(p, null, null, null, null, null, null, null, null, null);
        foreach (var previous in View.People.Where(item => item.Id == p.Id).ToArray()) View.People.Remove(previous);
        View.People.Add(row);
        View.SelectedPerson = row;
    }

    public void Register(params string[] ids)
    {
        foreach (var id in ids)
            Actions.Register(new GameActionDefinition
            {
                Id = id, Label = id, Description = "Description", Mode = ActionExecutionMode.Queued,
                IsAvailable = _ => true, Execute = _ => new GameActionResult(true)
            });
    }

    public void Dispose() => Random.AssertComplete();

    internal sealed class SuccessionStub(IPerson head) : ISuccessionService
    {
        public bool IsGameOver { get; set; }
        public int? MaleLineEndedYear => null;
        public bool DynastyLeftPoland => false;
        public int? DynastyLeftPolandYear => null;
        public Guid? ActiveControllerId => head.Id;
        public IPerson? ActiveController => head;
        public bool IsControllable(IPerson person) => person.Id == head.Id;
        public bool HasLivingMaleLineage => true;
        public bool SetActiveController(IPerson person) => throw new NotSupportedException();
        public void Refresh() => StateChanged?.Invoke(this, EventArgs.Empty);
        public event EventHandler? StateChanged;
    }

    internal sealed class LocationsStub(TownInfo residence) : ILocationService
    {
        public List<TownInfo> Towns { get; } = [residence];
        public LocationSnapshot GetLocation(IPerson person) => new(residence, residence, null);
        public TownInfo ChoosePropertyTown(IPerson head) => throw new NotSupportedException();
        public IReadOnlyList<TownInfo> GetTowns() => Towns;
        public TownInfo? FindTown(string id) => Towns.FirstOrDefault(town => town.Id == id);
        public void SetPersonHomeTown(IPerson person, TownInfo town) => throw new NotSupportedException();
        public void SetHouseholdHomeTown(IPerson head, TownInfo town) => throw new NotSupportedException();
    }

    private sealed class Opportunities(LocationsStub locations) : ILocalCareerOpportunityService
    {
        public CareerLocationEvaluation Evaluate(IPerson person, CareerLocationRequirement requirement) => throw new NotSupportedException();
        public CareerLocationEvaluation Evaluate(TownInfo town, CareerLocationRequirement requirement) => throw new NotSupportedException();
        public LocationOpportunitySnapshot GetOpportunitySnapshot(IPerson person) => GetOpportunitySnapshot(locations.GetLocation(person).HomeTown);
        public LocationOpportunitySnapshot GetOpportunitySnapshot(TownInfo town) => new(town, "Region",
            ["industry.trade", "industry.heavy_industry"], ["industry.arts", "industry.trade"], "Fixture");
    }

    internal sealed class EconomyStub
        : IEconomyService
    {
        public EconomyStub(IPerson head, TownInfo residence)
        { Head = head; Residence = residence; Members.Add(head.Id); }
        public IPerson Head { get; }
        public TownInfo Residence { get; }
        public HashSet<Guid> Members { get; } = [];
        public List<HousePropertyInfo> Houses { get; } = [];
        public int PriceReads { get; private set; }
        public decimal Wealth { get; set; } = 10000m;
        private decimal Price(TownInfo town) { PriceReads++; return 40000m; }
        private HouseholdFinanceSnapshot Finance => new(Wealth, Houses.Count, 0, 0m, 0, null, 0m, 0m, [], [], Houses);
        public bool HasHousehold(IPerson person) => person.Id == Head.Id;
        public void EnsureHousehold(IPerson person) => throw new NotSupportedException("Unused economy fixture operation: EnsureHousehold");
        public void EnsureIndependentHousehold(IPerson person, IPerson? dynastyAnchor = null) => throw new NotSupportedException("Unused economy fixture operation: EnsureIndependentHousehold");
        public HouseholdFinanceSnapshot? GetHousehold(IPerson person) => Members.Contains(person.Id) ? Finance : null;
        public bool CanAfford(IPerson person, decimal amount) => Finance.Wealth >= amount;
        public decimal GetProjectedAnnualIncome(IPerson person) => 0m;
        public IReadOnlyList<FinanceBreakdownItem> GetProjectedIncomeBreakdown(IPerson person) => [];
        public HouseholdAnnualForecast? GetAnnualForecast(IPerson person) => null;
        public Guid? GetHouseholdId(IPerson person) => Members.Contains(person.Id) ? Head.Id : null;
        public Guid? GetHouseholdDynastyAnchorId(IPerson person) => Members.Contains(person.Id) ? Head.Id : null;
        public IReadOnlyList<Guid> GetHouseholdMemberIds(IPerson person) => Members.ToArray();
        public bool IsLegacyMembershipSeeded(IPerson householdRepresentative) => true;
        public void MarkLegacyMembershipSeeded(IPerson householdRepresentative) => throw new NotSupportedException("Unused economy fixture operation: MarkLegacyMembershipSeeded");
        public void AddHouseholdMember(IPerson householdRepresentative, IPerson member) => throw new NotSupportedException("Unused economy fixture operation: AddHouseholdMember");
        public void RemoveHouseholdMember(IPerson member) => throw new NotSupportedException("Unused economy fixture operation: RemoveHouseholdMember");
        public void TransferHouseholdHead(IPerson currentHead, IPerson newHead) => throw new NotSupportedException("Unused economy fixture operation: TransferHouseholdHead");
        public void MarkEstateReady(IPerson householdRepresentative, bool ready = true) => throw new NotSupportedException("Unused economy fixture operation: MarkEstateReady");
        public bool IsEstateReady(IPerson householdRepresentative) => false;
        public void DissolveHousehold(IPerson householdRepresentative) => throw new NotSupportedException("Unused economy fixture operation: DissolveHousehold");
        public TownInfo GetResidenceTown(IPerson person) => Residence;
        public void SetResidenceTown(IPerson person, TownInfo town) => throw new NotSupportedException("Unused economy fixture operation: SetResidenceTown");
        public void SetWealth(IPerson person, decimal wealth) => throw new NotSupportedException("Unused economy fixture operation: SetWealth");
        public void ChangeWealth(IPerson person, decimal amount) => throw new NotSupportedException("Unused economy fixture operation: ChangeWealth");
        public void ChangeWealthAllowDebt(IPerson person, decimal amount) => throw new NotSupportedException("Unused economy fixture operation: ChangeWealthAllowDebt");
        public void RecordRealizedExpense(IPerson person, string label, decimal amount) => throw new NotSupportedException("Unused economy fixture operation: RecordRealizedExpense");
        public void ApplyAnnualFinanceReceipt(IPerson recipient, string label, decimal amount) => throw new NotSupportedException("Unused economy fixture operation: ApplyAnnualFinanceReceipt");
        public void SetHousesOwned(IPerson person, int housesOwned) => throw new NotSupportedException("Unused economy fixture operation: SetHousesOwned");
        public void SetRentedHouses(IPerson person, int rentedHouses) => throw new NotSupportedException("Unused economy fixture operation: SetRentedHouses");
        public IReadOnlyList<HousePropertyInfo> GetHouses(IPerson person) => Houses;
        public HousePropertyInfo AddHouse(IPerson person, TownInfo? town = null) => throw new NotSupportedException("Unused economy fixture operation: AddHouse");
        public void AddExistingHouse(IPerson person, HousePropertyInfo house) => throw new NotSupportedException("Unused economy fixture operation: AddExistingHouse");
        public HousePropertyInfo? TakeAdditionalHouse(IPerson person) => throw new NotSupportedException("Unused economy fixture operation: TakeAdditionalHouse");
        public HousePropertyInfo? TakeHouse(IPerson person, Guid propertyId) => throw new NotSupportedException("Unused economy fixture operation: TakeHouse");
        public bool SetHouseInheritanceHeir(IPerson person, Guid propertyId, Guid? heirId) => throw new NotSupportedException("Unused economy fixture operation: SetHouseInheritanceHeir");
        public decimal GetHousePrice(TownInfo town) => Price(town);
        public decimal GetHouseSaleValue(TownInfo town) => 32000m;
        public decimal GetLivingCostPerPerson(TownInfo town) => 500m;
        public decimal GetResidenceRent(TownInfo town) => 1000m;
        public decimal GetRentalIncome(TownInfo town) => 1000m;
        public IReadOnlyList<HousePropertyInfo> TakeAllHouses(IPerson person) => throw new NotSupportedException("Unused economy fixture operation: TakeAllHouses");
        public IReadOnlyList<FarmlandAssetInfo> GetFarmland(IPerson person) => [];
        public FarmlandAssetInfo AddFarmland(IPerson person, TownInfo town, int acquiredYear, string acquisitionSource) => throw new NotSupportedException("Unused economy fixture operation: AddFarmland");
        public void AddExistingFarmland(IPerson person, FarmlandAssetInfo farmland) => throw new NotSupportedException("Unused economy fixture operation: AddExistingFarmland");
        public FarmlandAssetInfo? TakeFarmland(IPerson person, Guid farmlandId) => throw new NotSupportedException("Unused economy fixture operation: TakeFarmland");
        public bool SetFarmlandInheritanceHeir(IPerson person, Guid farmlandId, Guid? heirId) => throw new NotSupportedException("Unused economy fixture operation: SetFarmlandInheritanceHeir");
        public IReadOnlyList<FarmlandAssetInfo> TakeAllFarmland(IPerson person) => throw new NotSupportedException("Unused economy fixture operation: TakeAllFarmland");
        public decimal GetPendingInheritance(IPerson person) => 0m;
        public void SetPendingInheritance(IPerson person, decimal amount) => throw new NotSupportedException("Unused economy fixture operation: SetPendingInheritance");
        public void ChangePendingInheritance(IPerson person, decimal amount) => throw new NotSupportedException("Unused economy fixture operation: ChangePendingInheritance");
        public int GetPendingHouses(IPerson person) => 0;
        public void SetPendingHouses(IPerson person, int houses) => throw new NotSupportedException("Unused economy fixture operation: SetPendingHouses");
        public void ChangePendingHouses(IPerson person, int houses) => throw new NotSupportedException("Unused economy fixture operation: ChangePendingHouses");
        public void AddPendingHouse(IPerson person, HousePropertyInfo house) => throw new NotSupportedException("Unused economy fixture operation: AddPendingHouse");
        public IReadOnlyList<HousePropertyInfo> TakePendingHouses(IPerson person) => throw new NotSupportedException("Unused economy fixture operation: TakePendingHouses");
        public IReadOnlyList<FarmlandAssetInfo> GetPendingFarmland(IPerson person) => [];
        public void AddPendingFarmland(IPerson person, FarmlandAssetInfo farmland) => throw new NotSupportedException("Unused economy fixture operation: AddPendingFarmland");
        public IReadOnlyList<FarmlandAssetInfo> TakePendingFarmland(IPerson person) => throw new NotSupportedException("Unused economy fixture operation: TakePendingFarmland");
        public void SetNanny(IPerson person, Guid? nannyId) => throw new NotSupportedException("Unused economy fixture operation: SetNanny");
        public IReadOnlyList<Guid> GetHostedDependentIds(IPerson householdHead) => [];
        public void AddHostedDependent(IPerson householdHead, IPerson dependent) => throw new NotSupportedException("Unused economy fixture operation: AddHostedDependent");
        public void RemoveHostedDependent(IPerson householdHead, IPerson dependent) => throw new NotSupportedException("Unused economy fixture operation: RemoveHostedDependent");
    }
}
