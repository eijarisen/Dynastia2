using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Data;
using Dynastia.Core.Events;
using Dynastia.Core.Plugins;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Economy;
using Dynastia.Mechanics.Heirlooms;
using Dynastia.Mechanics.Inheritance;

namespace Dynastia.Core.Tests;

public sealed class HeirloomsBatch1Tests
{
    [Fact]
    public void ServiceAddsListsTakesAndTracksCreationProvenance()
    {
        var fixture = CreateFixture();

        var item = fixture.Heirlooms.Create(
            fixture.Head,
            new HeirloomCreationRequest(
                "academic_university_diploma",
                fixture.Head.Id,
                "test",
                "education5",
                "Earned by completing university."));

        var listed = Assert.Single(fixture.Heirlooms.GetHeirlooms(fixture.Head));
        Assert.Equal(item.Id, listed.Id);
        Assert.Equal(fixture.Head.Id, listed.OriginPersonId);
        Assert.Equal(1900, listed.AcquiredYear);
        Assert.InRange(listed.AppraisedValue, 1620m, 1980m);
        var origin = Assert.Single(listed.OwnershipHistory);
        Assert.Equal("created", origin.Reason);
        Assert.Equal(fixture.Economy.GetHouseholdId(fixture.Head), origin.HouseholdId);

        var taken = fixture.Heirlooms.Take(fixture.Head, item.Id);
        Assert.NotNull(taken);
        Assert.Empty(fixture.Heirlooms.GetHeirlooms(fixture.Head));
    }

    [Fact]
    public void QueuedSaleRevalidatesOwnershipAndSoldItemIsRemovedPermanently()
    {
        var fixture = CreateFixture(initializePlugin: true);
        var actions = fixture.Actions!;
        var service = fixture.PluginHeirlooms!;
        var initialWealth = fixture.Economy.GetHousehold(fixture.Head)!.Wealth;

        var removedBeforeExecution = service.Create(
            fixture.Head,
            Request("academic_university_diploma", fixture.Head));

        var queued = actions.Execute(
            "heirloom.sell",
            fixture.Head,
            fixture.Head,
            new Dictionary<string, string>
            {
                ["heirloomId"] = removedBeforeExecution.Id.ToString()
            });
        Assert.True(queued.Success);

        Assert.NotNull(service.Take(fixture.Head, removedBeforeExecution.Id));
        var invalidated = Assert.Single(actions.ExecuteQueued(YearPhase.QueuedActionsEarly));
        Assert.Equal(QueuedActionResultCategory.Invalidated, invalidated.Category);
        Assert.Equal(initialWealth, fixture.Economy.GetHousehold(fixture.Head)!.Wealth);

        var sold = service.Create(
            fixture.Head,
            Request("academic_bound_dissertation", fixture.Head));
        var expectedSale = service.GetSaleValue(sold);

        Assert.True(actions.Execute(
            "heirloom.sell",
            fixture.Head,
            fixture.Head,
            new Dictionary<string, string>
            {
                ["heirloomId"] = sold.Id.ToString()
            }).Success);

        var outcome = Assert.Single(actions.ExecuteQueued(YearPhase.QueuedActionsEarly));
        Assert.Equal(QueuedActionResultCategory.ExecutedSuccessfully, outcome.Category);
        Assert.DoesNotContain(service.GetHeirlooms(fixture.Head), item => item.Id == sold.Id);
        Assert.DoesNotContain(service.TakeAll(fixture.Head), item => item.Id == sold.Id);
        Assert.Equal(initialWealth + expectedSale, fixture.Economy.GetHousehold(fixture.Head)!.Wealth);
        Assert.Contains(fixture.Events.AllEvents, item =>
            item.Type == "heirloom.sold"
            && item.Data.TryGetValue("heirloomId", out var id)
            && id == sold.Id.ToString());
    }

    [Fact]
    public void EstateHonorsDesignationRoundRobinPendingInheritanceAndHistoryTransfer()
    {
        var fixture = CreateFixture();
        var adult = fixture.State.CreatePerson("Adam", "Nowak", 25);
        var minor = fixture.State.CreatePerson("Piotr", "Nowak", 14);
        adult.Tags.Add("state.alive");
        minor.Tags.Add("state.alive");
        fixture.Family.SetChildren(fixture.Head, adult, minor);

        AttachHousehold(adult, fixture.Town, 100m);

        var designated = fixture.Heirlooms.Create(
            fixture.Head,
            Request("academic_university_diploma", fixture.Head));
        var unassigned = fixture.Heirlooms.Create(
            fixture.Head,
            Request("academic_bound_dissertation", fixture.Head));
        var pending = fixture.Heirlooms.Create(
            fixture.Head,
            Request("academic_annotated_reference", fixture.Head));

        Assert.True(fixture.Heirlooms.SetInheritanceHeir(
            fixture.Head,
            designated.Id,
            adult.Id));
        Assert.True(fixture.Heirlooms.SetInheritanceHeir(
            fixture.Head,
            pending.Id,
            minor.Id));

        fixture.Head.Tags.Remove("state.alive");
        fixture.Economy.MarkEstateReady(fixture.Head);

        new EstateInheritanceSystem(
            fixture.Family,
            fixture.Economy,
            fixture.Heirlooms,
            fixture.Events)
            .Execute(fixture.State);

        var adultItems = fixture.Heirlooms.GetHeirlooms(adult);
        Assert.Contains(adultItems, item => item.Id == designated.Id);
        // The sole unassigned item uses the same round-robin sequence as Houses/Farmland.
        Assert.Contains(adultItems, item => item.Id == unassigned.Id);

        var pendingItems = fixture.Heirlooms.GetPending(minor);
        var pendingItem = Assert.Single(pendingItems);
        Assert.Equal(pending.Id, pendingItem.Id);
        Assert.Null(pendingItem.AssignedHeirId);

        var inherited = adultItems.Single(item => item.Id == designated.Id);
        Assert.Null(inherited.AssignedHeirId);
        Assert.Equal("created", inherited.OwnershipHistory[0].Reason);
        Assert.Equal("inherited", inherited.OwnershipHistory[^1].Reason);
        Assert.Equal(adult.Id, inherited.OwnershipHistory[^1].PersonId);

        Assert.Empty(fixture.Heirlooms.GetHeirlooms(fixture.Head));
        Assert.Contains(fixture.Events.AllEvents, item => item.Type == "heirloom.inherited");
        Assert.Contains(fixture.Events.AllEvents, item => item.Type == "heirloom.pending");
    }

    [Fact]
    public void PendingHeirloomMovesIntoEstablishedHouseholdAndAddsHistory()
    {
        var fixture = CreateFixture();
        var child = fixture.State.CreatePerson("Adam", "Nowak", 18);
        child.Tags.Add("state.alive");
        fixture.Family.SetChildren(fixture.Head, child);
        AttachHousehold(child, fixture.Town, 0m);

        var item = fixture.Heirlooms.Create(
            fixture.Head,
            Request("academic_university_diploma", fixture.Head));
        Assert.NotNull(fixture.Heirlooms.Take(fixture.Head, item.Id));
        fixture.Heirlooms.AddPending(child, item, fixture.State.Year, "inherited_pending");

        new AdulthoodInheritanceSystem(
            fixture.Family,
            fixture.Economy,
            fixture.Heirlooms,
            fixture.Events)
            .Execute(fixture.State);

        Assert.Empty(fixture.Heirlooms.GetPending(child));
        var received = Assert.Single(fixture.Heirlooms.GetHeirlooms(child));
        Assert.Equal(item.Id, received.Id);
        Assert.Equal("inherited", received.OwnershipHistory[^1].Reason);
    }

    private static HeirloomCreationRequest Request(string templateId, IPerson person) =>
        new(
            templateId,
            person.Id,
            "test",
            "test.trigger",
            "Test provenance.");

    private static Fixture CreateFixture(bool initializePlugin = false)
    {
        var random = new DeterministicRandom();
        var state = new GameState(random)
        {
            Year = 1900,
            StartYear = 1900
        };
        var head = state.CreatePerson("Jan", "Nowak", 45);
        head.Tags.Add("state.alive");
        head.Tags.Add("control.playable");

        var town = new TownInfo("Testowo", "Test", 20.0, 52.0, 10000)
        {
            Id = "test-town",
            RegionId = "test-region"
        };
        var family = new TestFamilyService();
        var economy = new StandardEconomyService(
            state,
            family,
            new TestLocationService(town),
            new IncomeProviderRegistry(),
            new HouseholdIncomeProviderRegistry(),
            new TestProjectionRegistry(),
            new TestStatsService(),
            random);
        AttachHousehold(head, town, 1000m);

        var events = new GameEventBus();
        var data = new JsonGameDataService(Path.Combine(FindRepoRoot(), "data"));
        var catalog = HeirloomCatalog.Load(data);
        var service = new StandardHeirloomService(
            state,
            economy,
            family,
            random,
            events,
            catalog);

        if (!initializePlugin)
            return new Fixture(state, head, town, family, economy, events, service, null, null);

        var actions = new ActionRegistry(
            state,
            events,
            random,
            new ActionGuardRegistry());
        var context = new GamePluginContext();
        context.AddService<IGameState>(state);
        context.AddService<IEconomyService>(economy);
        context.AddService<IFamilyService>(family);
        context.AddService<IWorkCapacityService>(new FullWorkCapacityService());
        context.AddService<IGameRandom>(random);
        context.AddService<IGameEventBus>(events);
        context.AddService<IActionRegistry>(actions);
        context.AddService<IGameDataService>(data);
        new HeirloomsPlugin().Initialize(context);

        return new Fixture(
            state,
            head,
            town,
            family,
            economy,
            events,
            service,
            actions,
            context.GetService<IHeirloomService>()!);
    }

    private static void AttachHousehold(IPerson head, TownInfo town, decimal wealth)
    {
        var component = new HouseholdEconomyComponent
        {
            HouseholdId = Guid.NewGuid(),
            HeadId = head.Id,
            DynastyAnchorId = head.Id,
            ResidenceTownId = town.Id,
            Wealth = wealth,
            LegacyMembershipSeeded = true
        };
        component.MemberIds.Add(head.Id);
        head.Components.Set(component);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dynastia.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Dynastia repository root.");
    }

    private sealed record Fixture(
        GameState State,
        IPerson Head,
        TownInfo Town,
        TestFamilyService Family,
        StandardEconomyService Economy,
        GameEventBus Events,
        StandardHeirloomService Heirlooms,
        ActionRegistry? Actions,
        IHeirloomService? PluginHeirlooms);

    private sealed class DeterministicRandom : IGameRandom
    {
        private int _value;
        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            var span = maxInclusive - minInclusive + 1;
            return minInclusive + Math.Abs(_value++ % span);
        }

        public double NextDouble() => 0.5;
        public bool Chance(double probability) => probability >= 1.0;
    }


    private sealed class FullWorkCapacityService : IWorkCapacityService
    {
        public WorkCapacitySnapshot GetWorkCapacity(IPerson person) =>
            new(1.0, true);
    }

    private sealed class TestProjectionRegistry : IHouseholdFinanceProjectionProviderRegistry
    {
        private readonly List<IHouseholdFinanceProjectionProvider> _providers = [];
        public IReadOnlyCollection<IHouseholdFinanceProjectionProvider> Providers => _providers;
        public void Register(IHouseholdFinanceProjectionProvider provider) => _providers.Add(provider);
    }

    private sealed class TestLocationService : ILocationService
    {
        private readonly TownInfo _town;
        public TestLocationService(TownInfo town) => _town = town;
        public LocationSnapshot GetLocation(IPerson person) => new(_town, _town, null);
        public TownInfo ChoosePropertyTown(IPerson householdHead) => _town;
        public IReadOnlyList<TownInfo> GetTowns() => [_town];
        public TownInfo? FindTown(string townId) => townId == _town.Id ? _town : null;
        public void SetPersonHomeTown(IPerson person, TownInfo town) { }
        public void SetHouseholdHomeTown(IPerson householdHead, TownInfo town) { }
    }

    private sealed class TestStatsService : IStatsService
    {
        private static readonly IReadOnlyList<StatValue> Values =
            [new StatValue("intellect", "Intellect", 3, string.Empty)];
        public IReadOnlyList<StatValue> GetStats(IPerson person) => Values;
        public IReadOnlyList<StatValue> GetBaseStats(IPerson person) => Values;
        public void EnsureStats(IPerson person) { }
        public void SetStats(IPerson person, IReadOnlyDictionary<string, int> values) { }
        public bool TryIncreaseAcquiredStat(IPerson person, string statId) => false;
    }

    private sealed class TestFamilyService : IFamilyService
    {
        private readonly Dictionary<Guid, List<IPerson>> _children = [];
        private readonly Dictionary<Guid, IPerson> _fathers = [];

        public void SetChildren(IPerson parent, params IPerson[] children)
        {
            _children[parent.Id] = children.ToList();
            foreach (var child in children)
                _fathers[child.Id] = parent;
        }

        public void InitializePerson(IPerson person, Sex sex, int? generation = null) { }
        public Sex GetSex(IPerson person) => Sex.Male;
        public int? GetGeneration(IPerson person) => 1;
        public IPerson? GetFather(IPerson person) =>
            _fathers.TryGetValue(person.Id, out var father) ? father : null;
        public IPerson? GetMother(IPerson person) => null;
        public IPerson? GetSpouse(IPerson person) => null;
        public IReadOnlyList<IPerson> GetChildren(IPerson person) =>
            _children.TryGetValue(person.Id, out var result) ? result : Array.Empty<IPerson>();
        public void SetParents(IPerson child, IPerson? father, IPerson? mother) { }
        public void SetSpouses(IPerson first, IPerson second, int startYear) { }
        public void EndRelationship(IPerson first, IPerson second, int endYear, string endReason, bool clearFirst = true, bool clearSecond = true) { }
        public IReadOnlyList<RelationshipHistoryInfo> GetRelationshipHistory(IPerson person) => [];
        public void SetGeneratedFamilyBackground(IPerson person, GeneratedFamilyBackgroundInfo background) { }
        public GeneratedFamilyBackgroundInfo? GetGeneratedFamilyBackground(IPerson person) => null;
        public string FormatSurname(string surname, Sex sex) => surname;
        public string GetDisplayName(IPerson person) => $"{person.Name} {person.Surname}";
        public bool IsBloodline(IPerson person) => true;
        public bool IsMaleLineage(IPerson person) => true;
    }
}
