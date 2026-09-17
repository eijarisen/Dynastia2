using Dynastia.Contracts;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Economy;

namespace Dynastia.Core.Tests;

public sealed class SharedMechanics4DEconomyConsolidationTests
{
    [Fact]
    public void ForecastAndRealizedFinanceShareTheSameHouseholdCompositionRules()
    {
        var state = new GameState();
        var head = state.CreatePerson("Jan", "Nowak", 40);
        var spouse = state.CreatePerson("Anna", "Nowak", 38);
        head.Tags.Add("state.alive");
        spouse.Tags.Add("state.alive");

        var town = TestTown();
        var income = new IncomeProviderRegistry();
        income.Register(new FixedPersonIncomeProvider(head.Id, 1000m, 1100m));
        income.Register(new FixedPersonIncomeProvider(spouse.Id, 500m, 600m));

        var householdIncome = new HouseholdIncomeProviderRegistry();
        householdIncome.Register(new FixedHouseholdIncomeProvider(700m, 900m));

        var projections = new TestProjectionRegistry();
        projections.Register(new FixedProjectionProvider(300m, 400m));

        var economy = new StandardEconomyService(
            state,
            new TestFamilyService(),
            new TestLocationService(town),
            income,
            householdIncome,
            projections,
            new TestStatsService(),
            new FixedRandom());

        var component = new HouseholdEconomyComponent
        {
            HouseholdId = Guid.NewGuid(),
            HeadId = head.Id,
            DynastyAnchorId = head.Id,
            ResidenceTownId = town.Id,
            Wealth = 5000m,
            LegacyMembershipSeeded = true
        };
        component.MemberIds.Add(head.Id);
        component.MemberIds.Add(spouse.Id);
        head.Components.Set(component);

        var forecast = economy.GetAnnualForecast(head);

        Assert.NotNull(forecast);
        Assert.Equal(2500m, forecast!.ProjectedIncome);
        Assert.Equal(1326m, forecast.ProjectedExpenses);
        Assert.Equal(2200m, economy.GetProjectedAnnualIncome(head));
        Assert.Contains(forecast.IncomeBreakdown, line => line.Label == "loan projection" && line.Amount == 300m);
        Assert.Contains(forecast.ExpenseBreakdown, line => line.Label == "living costs" && line.Amount == 476m);
        Assert.Contains(forecast.ExpenseBreakdown, line => line.Label == "rented home" && line.Amount == 450m);
        Assert.Contains(forecast.ExpenseBreakdown, line => line.Label == "loan projection" && line.Amount == 400m);

        var yearSystem = new EconomyYearSystem(
            economy,
            new TestFamilyService(),
            new TestEventBus());

        yearSystem.Execute(state);

        var realized = economy.GetHousehold(head);
        Assert.NotNull(realized);
        Assert.Equal(2600m, realized!.LastIncome);
        Assert.Equal(926m, realized.LastExpenses);
        Assert.Equal(6674m, realized.Wealth);
        Assert.Contains(realized.LastIncomeBreakdown, line => line.Label == "Jan" && line.Amount == 1100m);
        Assert.Contains(realized.LastIncomeBreakdown, line => line.Label == "Anna" && line.Amount == 600m);
        Assert.Contains(realized.LastIncomeBreakdown, line => line.Label == "farming/crafts" && line.Amount == 900m);
        Assert.Contains(realized.LastExpenseBreakdown, line => line.Label == "living costs" && line.Amount == 476m);
        Assert.Contains(realized.LastExpenseBreakdown, line => line.Label == "rented home" && line.Amount == 450m);
    }

    [Fact]
    public void ForecastUsesTheSameAuthoritativeRentalIncomeRule()
    {
        var state = new GameState();
        var head = state.CreatePerson("Jan", "Nowak", 40);
        head.Tags.Add("state.alive");
        var homeTown = TestTown();
        var rentalTown = new TownInfo("Rentowo", "Test", 20.5, 52.2, 10000)
        {
            Id = "rental-town",
            RegionId = "test-region"
        };

        var economy = new StandardEconomyService(
            state,
            new TestFamilyService(),
            new TestLocationService(homeTown, rentalTown),
            new IncomeProviderRegistry(),
            new HouseholdIncomeProviderRegistry(),
            new TestProjectionRegistry(),
            new TestStatsService(),
            new FixedRandom());

        var component = new HouseholdEconomyComponent
        {
            HouseholdId = Guid.NewGuid(),
            HeadId = head.Id,
            DynastyAnchorId = head.Id,
            ResidenceTownId = homeTown.Id,
            LegacyMembershipSeeded = true
        };
        component.MemberIds.Add(head.Id);
        component.Houses.Add(new HousePropertyState { Id = Guid.NewGuid(), Town = homeTown });
        component.Houses.Add(new HousePropertyState { Id = Guid.NewGuid(), Town = rentalTown });
        head.Components.Set(component);

        var forecast = economy.GetAnnualForecast(head);

        Assert.NotNull(forecast);
        Assert.Equal(economy.GetRentalIncome(rentalTown), forecast!.ProjectedIncome);
        Assert.Equal(238m, forecast.ProjectedExpenses);
        Assert.Contains(forecast.IncomeBreakdown, line => line.Label == "houses" && line.Amount == 450m);
        Assert.DoesNotContain(forecast.ExpenseBreakdown, line => line.Label == "rented home");
    }

    [Fact]
    public void AffordabilityUsesTheAuthoritativeHouseholdBalance()
    {
        var state = new GameState();
        var head = state.CreatePerson("Jan", "Nowak", 40);
        head.Tags.Add("state.alive");
        var town = TestTown();

        var economy = new StandardEconomyService(
            state,
            new TestFamilyService(),
            new TestLocationService(town),
            new IncomeProviderRegistry(),
            new HouseholdIncomeProviderRegistry(),
            new TestProjectionRegistry(),
            new TestStatsService(),
            new FixedRandom());

        head.Components.Set(new HouseholdEconomyComponent
        {
            HouseholdId = Guid.NewGuid(),
            HeadId = head.Id,
            DynastyAnchorId = head.Id,
            ResidenceTownId = town.Id,
            Wealth = 3000m,
            LegacyMembershipSeeded = true
        });
        head.Components.Get<HouseholdEconomyComponent>()!.MemberIds.Add(head.Id);

        Assert.True(economy.CanAfford(head, 3000m));
        Assert.False(economy.CanAfford(head, 3001m));
        Assert.False(economy.CanAfford(head, -1m));
    }

    private static TownInfo TestTown() =>
        new("Testowo", "Test", 20.0, 52.0, 10000)
        {
            Id = "test-town",
            RegionId = "test-region"
        };

    private sealed class FixedPersonIncomeProvider : IIncomeProvider
    {
        private readonly Guid _personId;
        private readonly decimal _expected;
        private readonly decimal _realized;

        public FixedPersonIncomeProvider(Guid personId, decimal expected, decimal realized)
        {
            _personId = personId;
            _expected = expected;
            _realized = realized;
        }

        public string Id => $"test.person.{_personId}";

        public decimal GetAnnualIncome(IPerson person) =>
            person.Id == _personId ? _realized : 0m;

        public decimal GetExpectedAnnualIncome(IPerson person) =>
            person.Id == _personId ? _expected : 0m;
    }

    private sealed class FixedHouseholdIncomeProvider : IHouseholdIncomeProvider
    {
        private readonly decimal _expected;
        private readonly decimal _realized;

        public FixedHouseholdIncomeProvider(decimal expected, decimal realized)
        {
            _expected = expected;
            _realized = realized;
        }

        public string Id => "test.household";
        public string Label => "farming/crafts";
        public decimal GetAnnualIncome(IPerson householdRepresentative) => _realized;
        public decimal GetExpectedAnnualIncome(IPerson householdRepresentative) => _expected;
    }

    private sealed class FixedProjectionProvider : IHouseholdFinanceProjectionProvider
    {
        private readonly decimal _income;
        private readonly decimal _expense;

        public FixedProjectionProvider(decimal income, decimal expense)
        {
            _income = income;
            _expense = expense;
        }

        public string Id => "test.projection";

        public IReadOnlyList<FinanceBreakdownItem> GetProjectedIncome(IPerson householdRepresentative) =>
            [new FinanceBreakdownItem("loan projection", _income)];

        public IReadOnlyList<FinanceBreakdownItem> GetProjectedExpenses(IPerson householdRepresentative) =>
            [new FinanceBreakdownItem("loan projection", _expense)];
    }

    private sealed class TestProjectionRegistry : IHouseholdFinanceProjectionProviderRegistry
    {
        private readonly List<IHouseholdFinanceProjectionProvider> _providers = [];
        public IReadOnlyCollection<IHouseholdFinanceProjectionProvider> Providers => _providers;
        public void Register(IHouseholdFinanceProjectionProvider provider) => _providers.Add(provider);
    }

    private sealed class TestLocationService : ILocationService
    {
        private readonly TownInfo _homeTown;
        private readonly IReadOnlyList<TownInfo> _towns;

        public TestLocationService(TownInfo homeTown, params TownInfo[] otherTowns)
        {
            _homeTown = homeTown;
            _towns = [homeTown, .. otherTowns];
        }

        public LocationSnapshot GetLocation(IPerson person) =>
            new(_homeTown, _homeTown, null);
        public TownInfo ChoosePropertyTown(IPerson householdHead) => _homeTown;
        public IReadOnlyList<TownInfo> GetTowns() => _towns;
        public TownInfo? FindTown(string townId) =>
            _towns.FirstOrDefault(town => town.Id == townId);
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

    private sealed class FixedRandom : IGameRandom
    {
        public int NextInt(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => 1.0;
        public bool Chance(double probability) => false;
    }

    private sealed class TestEventBus : IGameEventBus
    {
        private readonly List<GameEvent> _events = [];
        public event EventHandler<GameEvent>? EventPublished;
        public IReadOnlyList<GameEvent> AllEvents => _events;
        public void Publish(GameEvent gameEvent)
        {
            _events.Add(gameEvent);
            EventPublished?.Invoke(this, gameEvent);
        }
        public IReadOnlyList<GameEvent> GetEventsForYear(int year) =>
            _events.Where(item => item.Year == year).ToList();
        public void RestoreEvents(IReadOnlyList<GameEvent> events)
        {
            _events.Clear();
            _events.AddRange(events);
        }
    }

    private sealed class TestFamilyService : IFamilyService
    {
        public void InitializePerson(IPerson person, Sex sex, int? generation = null) { }
        public Sex GetSex(IPerson person) => Sex.Male;
        public int? GetGeneration(IPerson person) => 1;
        public IPerson? GetFather(IPerson person) => null;
        public IPerson? GetMother(IPerson person) => null;
        public IPerson? GetSpouse(IPerson person) => null;
        public IReadOnlyList<IPerson> GetChildren(IPerson person) => Array.Empty<IPerson>();
        public void SetParents(IPerson child, IPerson? father, IPerson? mother) { }
        public void SetSpouses(IPerson first, IPerson second, int startYear) { }
        public void EndRelationship(IPerson first, IPerson second, int endYear, string endReason, bool clearFirst = true, bool clearSecond = true) { }
        public IReadOnlyList<RelationshipHistoryInfo> GetRelationshipHistory(IPerson person) => Array.Empty<RelationshipHistoryInfo>();
        public void SetGeneratedFamilyBackground(IPerson person, GeneratedFamilyBackgroundInfo background) { }
        public GeneratedFamilyBackgroundInfo? GetGeneratedFamilyBackground(IPerson person) => null;
        public string FormatSurname(string surname, Sex sex) => surname;
        public string GetDisplayName(IPerson person) => $"{person.Name} {person.Surname}";
        public bool IsBloodline(IPerson person) => true;
        public bool IsMaleLineage(IPerson person) => true;
    }
}
