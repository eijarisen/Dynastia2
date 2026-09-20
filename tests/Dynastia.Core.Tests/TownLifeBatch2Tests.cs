using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.TownLife;

namespace Dynastia.Core.Tests;

public sealed class TownLifeBatch2Tests
{
    [Theory]
    [InlineData(75, LocalEconomicStrength.Strong, 0.9175)]
    [InlineData(75, LocalEconomicStrength.Supported, 0.8875)]
    [InlineData(75, LocalEconomicStrength.Normal, 0.85)]
    [InlineData(75, LocalEconomicStrength.Weak, 0.805)]
    [InlineData(100, LocalEconomicStrength.Strong, 1.0)]
    [InlineData(100, LocalEconomicStrength.Supported, 1.0)]
    [InlineData(100, LocalEconomicStrength.Normal, 1.0)]
    [InlineData(100, LocalEconomicStrength.Weak, 1.0)]
    [InlineData(125, LocalEconomicStrength.Strong, 1.1725)]
    [InlineData(125, LocalEconomicStrength.Supported, 1.1575)]
    [InlineData(125, LocalEconomicStrength.Normal, 1.15)]
    [InlineData(125, LocalEconomicStrength.Weak, 1.105)]
    public void ProsperityMultiplierMatchesApprovedStrengthCurve(
        int prosperityIndex,
        LocalEconomicStrength strength,
        double expected)
    {
        var (state, service, town) = CreateProsperityService($"town-{prosperityIndex}-{strength}");
        var initial = service.Get(town).Index;
        service.ApplyHistoricalShock(
            [town.Id],
            "test.force_index",
            prosperityIndex - initial,
            20);

        Assert.Equal(
            Math.Round((decimal)expected, 4),
            Math.Round(service.GetIncomeMultiplier(town, strength), 4));
    }

    [Fact]
    public void EconomicStrengthUsesTownThenRegionThenWeakAndKeepsUnmappedNormal()
    {
        var data = DataService();
        var catalog = TownEconomicStrengthCatalog.Load(data);
        var town = Town("strength-town");

        var strong = new StandardLocalEconomicStrengthService(
            new StubOpportunityService(townTags: ["technology"]),
            catalog);
        Assert.Equal(
            LocalEconomicStrength.Strong,
            strong.ResolveCareer(town, "engineering"));

        var supported = new StandardLocalEconomicStrengthService(
            new StubOpportunityService(regionTags: ["finance"]),
            catalog);
        Assert.Equal(
            LocalEconomicStrength.Supported,
            supported.ResolveCareer(town, "commerce"));

        var weak = new StandardLocalEconomicStrengthService(
            new StubOpportunityService(),
            catalog);
        Assert.Equal(
            LocalEconomicStrength.Weak,
            weak.ResolveCareer(town, "engineering"));
        Assert.Equal(
            LocalEconomicStrength.Normal,
            weak.ResolveCareer(town, "education"));
        Assert.Equal(
            LocalEconomicStrength.Weak,
            weak.ResolveFarming(town));

        var farmingStrong = new StandardLocalEconomicStrengthService(
            new StubOpportunityService(townTags: ["agriculture"]),
            catalog);
        Assert.Equal(
            LocalEconomicStrength.Strong,
            farmingStrong.ResolveFarming(town));
    }

    [Fact]
    public void HistoricalShockRecoversLinearlyWithoutChangingBaseProsperity()
    {
        var (state, service, town) = CreateProsperityService("recovery-town");
        var baseline = service.Get(town).Index;

        service.ApplyHistoricalShock([town.Id], "test.recession", -10, 5);
        Assert.Equal(Math.Clamp(baseline - 10, 75, 125), service.Get(town).Index);

        state.Year += 1;
        Assert.Equal(Math.Clamp(baseline - 8, 75, 125), service.Get(town).Index);

        state.Year += 4;
        Assert.Equal(baseline, service.Get(town).Index);
        Assert.Empty(service.Get(town).ActiveShocks);
    }

    [Fact]
    public void ProsperityStateLivesOnPersistedAnchorComponentAcrossServiceRecreation()
    {
        var (state, service, town) = CreateProsperityService("persisted-town");
        var initial = service.Get(town).Index;
        service.ApplyHistoricalShock([town.Id], "test.persist", -5, 4);
        var expected = service.Get(town);

        var recreated = new StandardTownProsperityService(
            state,
            TownProsperityRules.Load(DataService()));
        var actual = recreated.Get(town);

        Assert.Equal(expected.Index, actual.Index);
        Assert.Equal(expected.ActiveShocks, actual.ActiveShocks);
        Assert.NotEqual(initial, expected.Index);
        Assert.NotNull(state.People[0].Components.Get<TownProsperityStateComponent>());
    }

    [Fact]
    public void ForecastReadsProsperityWithoutConsumingRandomAndAnnualDriftDoesConsumeRandom()
    {
        var (_, service, town) = CreateProsperityService("forecast-town");
        var random = new CountingRandom();

        _ = service.GetIncomeMultiplier(town, LocalEconomicStrength.Normal);
        _ = service.Get(town);
        Assert.Equal(0, random.CallCount);

        service.AdvanceTrackedTowns(random);
        Assert.Equal(0, random.CallCount); // no year has advanced yet
    }

    [Fact]
    public void OrdinaryDriftAndMeanReversionStayWithinThreePointsPerYear()
    {
        var (state, service, town) = CreateProsperityService("bounded-drift-town");
        _ = service.Get(town);
        var tracked = Assert.Single(service.GetTrackedStates());
        tracked.BaseIndex = 90;

        state.Year += 1;
        service.AdvanceTrackedTowns(new FixedDriftRandom(0.95, chanceResult: true));

        // 0.95 selects +2; successful mean reversion adds at most +1.
        Assert.Equal(93, tracked.BaseIndex);
        Assert.Equal(3, tracked.LastTrend);
    }

    [Fact]
    public void NextYearDriftConsumesRandomOnlyForTrackedTowns()
    {
        var (state, service, town) = CreateProsperityService("drift-town");
        var random = new CountingRandom();
        _ = service.Get(town);

        state.Year += 1;
        service.AdvanceTrackedTowns(random);

        Assert.True(random.CallCount >= 1);
        Assert.Single(service.GetTrackedStates());
    }


    [Fact]
    public void ProsperitySnapshotKeepsOneHistoryPointPerAdvancedYear()
    {
        var (state, service, town) = CreateProsperityService("history-town");
        var first = service.Get(town);
        Assert.Single(first.HistoryPoints);
        Assert.Equal(state.Year, first.HistoryPoints[0].Year);

        state.Year += 1;
        service.AdvanceTrackedTowns(new CountingRandom());
        var second = service.Get(town);

        Assert.Equal(2, second.HistoryPoints.Count);
        Assert.Equal(state.Year - 1, second.HistoryPoints[0].Year);
        Assert.Equal(state.Year, second.HistoryPoints[1].Year);
    }

    [Fact]
    public void CareerCraftAndFarmingIncomePathsAllUseTownProsperity()
    {
        var root = RepositoryRoot();
        var career = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Career",
            "StandardCareerService.Compensation.cs"));
        var crafts = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Crafts",
            "StandardCraftService.cs"));
        var farming = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Farming",
            "StandardFarmingService.cs"));

        Assert.Contains("_prosperity.GetIncomeMultiplier", career);
        Assert.Contains("ApplyTownIncomeMultiplier", crafts);
        Assert.Contains("_prosperity.GetIncomeMultiplier", crafts);
        Assert.Contains("ApplyTownIncomeMultiplier", farming);
        Assert.Contains("_prosperity.GetIncomeMultiplier", farming);
    }


    [Fact]
    public void HistoricalEventBridgeAppliesConfiguredShockAtEventStart()
    {
        var (state, service, town) = CreateProsperityService("shock-bridge-town");
        state.Year = 1929;
        var baseline = service.Get(town).Index;
        var context = new StubPluginContext();
        context.AddService<IHistoricalEventService>(
            new StubHistoricalEventService(
                "great_depression",
                1929,
                1933,
                [town.Id]));
        var system = new TownProsperityYearSystem(
            context,
            new CountingRandom(),
            service,
            [new HistoricalProsperityEffect("great_depression", -12, 6)]);

        system.Execute(state);

        Assert.Equal(
            Math.Clamp(baseline - 12, 75, 125),
            service.Get(town).Index);
        Assert.Contains("great_depression", service.Get(town).ActiveShocks);
    }

    [Fact]
    public void HistoricalEventBridgeRestoresRemainingShockWhenGameStartsMidRecovery()
    {
        var (state, service, town) = CreateProsperityService("mid-recovery-town");
        state.Year = 1932;
        var baseline = service.Get(town).Index;
        var context = new StubPluginContext();
        context.AddService<IHistoricalEventService>(
            new StubHistoricalEventService(
                "great_depression",
                1929,
                1935,
                [town.Id]));
        var system = new TownProsperityYearSystem(
            context,
            new CountingRandom(),
            service,
            [new HistoricalProsperityEffect("great_depression", -12, 6)]);

        system.ReconcileHistoricalEffects(state);

        // Three of six recovery years have elapsed: -12 -> -6.
        Assert.Equal(
            Math.Clamp(baseline - 6, 75, 125),
            service.Get(town).Index);
        Assert.Contains("great_depression", service.Get(town).ActiveShocks);
    }

    [Fact]
    public void Batch2TownLifeWindowShowsProsperityAndHistoricalShocksWithoutLegacyExplanations()
    {
        var root = RepositoryRoot();
        var window = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "Views",
            "TownLifeWindow.axaml"));

        Assert.DoesNotContain(
            "does not directly alter household income yet",
            window,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Local Economy", window);
        Assert.Contains("TownProsperityGraph", window);
        Assert.Contains("Prosperity.ActiveShocksText", window);
    }

    [Fact]
    public void HistoricalShockBridgeUsesYearAvailableTownsInsteadOfEveryPermanentPlace()
    {
        var root = RepositoryRoot();
        var service = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Historical",
            "HistoricalEventService.cs"));

        Assert.Contains("GetAvailableTowns(year)", service);
        Assert.DoesNotContain(
            "return _towns.PermanentPlaceIds",
            service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalEffectsAndCareerFamilyMappingsLoadFromBatch2Data()
    {
        var catalog = TownEconomicStrengthCatalog.Load(DataService());

        Assert.Equal(31, catalog.CareerFamilyTags.Count);
        Assert.Equal(21, catalog.HistoricalEffects.Count);
        Assert.Contains(catalog.HistoricalEffects, item =>
            item.EventId == "great_depression"
            && item.Delta == -12
            && item.RecoveryYears == 6);
    }

    private static (GameState State, StandardTownProsperityService Service, TownInfo Town)
        CreateProsperityService(string townId)
    {
        var state = new GameState
        {
            DynastySurname = "Nowak",
            StartYear = 1900,
            Year = 1900
        };
        state.CreatePerson("Jan", "Nowak", 30);
        var service = new StandardTownProsperityService(
            state,
            TownProsperityRules.Load(DataService()));
        return (state, service, Town(townId));
    }

    private static TownInfo Town(string id) =>
        new("Test Town", "Test County", 19.0, 52.0, 20_000)
        {
            Id = id,
            RegionId = "test_region",
            PolityId = "test_polity",
            PolityName = "Test Polity",
            IsDestinationAvailable = true
        };

    private static JsonGameDataService DataService() =>
        new(Path.Combine(RepositoryRoot(), "data"));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dynastia.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class CountingRandom : IGameRandom
    {
        public int CallCount { get; private set; }

        public int NextInt(int minInclusive, int maxInclusive)
        {
            CallCount++;
            return minInclusive;
        }

        public double NextDouble()
        {
            CallCount++;
            return 0.5;
        }

        public bool Chance(double probability)
        {
            CallCount++;
            return false;
        }
    }

    private sealed class FixedDriftRandom : IGameRandom
    {
        private readonly double _nextDouble;
        private readonly bool _chanceResult;

        public FixedDriftRandom(double nextDouble, bool chanceResult)
        {
            _nextDouble = nextDouble;
            _chanceResult = chanceResult;
        }

        public int NextInt(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => _nextDouble;
        public bool Chance(double probability) => _chanceResult;
    }

    private sealed class StubPluginContext : IGamePluginContext
    {
        private readonly Dictionary<Type, object> _services = [];

        public T? GetService<T>() where T : class =>
            _services.TryGetValue(typeof(T), out var service)
                ? (T)service
                : null;

        public void AddService<T>(T service) where T : class =>
            _services[typeof(T)] = service;

        public void Log(string message)
        {
        }
    }

    private sealed class StubHistoricalEventService : IHistoricalEventService
    {
        private readonly string _eventId;
        private readonly int _startYear;
        private readonly int _endYear;
        private readonly IReadOnlyCollection<string> _placeIds;

        public StubHistoricalEventService(
            string eventId,
            int startYear,
            int endYear,
            IReadOnlyCollection<string> placeIds)
        {
            _eventId = eventId;
            _startYear = startYear;
            _endYear = endYear;
            _placeIds = placeIds;
        }

        public bool IsEventActive(string eventId, int year) =>
            eventId.Equals(_eventId, StringComparison.OrdinalIgnoreCase)
            && year >= _startYear
            && year <= _endYear;

        public int? GetEventStartYear(string eventId) =>
            eventId.Equals(_eventId, StringComparison.OrdinalIgnoreCase)
                ? _startYear
                : null;

        public HistoricalResidenceSnapshot? GetExternalResidence(IPerson person) => null;

        public IReadOnlyCollection<string> GetAffectedPlaceIds(string eventId, int year) =>
            IsEventActive(eventId, year)
                ? _placeIds
                : Array.Empty<string>();
    }

    private sealed class StubOpportunityService : ILocalCareerOpportunityService
    {
        private readonly IReadOnlyList<string> _townTags;
        private readonly IReadOnlyList<string> _regionTags;

        public StubOpportunityService(
            IReadOnlyList<string>? townTags = null,
            IReadOnlyList<string>? regionTags = null)
        {
            _townTags = townTags ?? Array.Empty<string>();
            _regionTags = regionTags ?? Array.Empty<string>();
        }

        public CareerLocationEvaluation Evaluate(
            IPerson person,
            CareerLocationRequirement requirement) =>
            throw new NotSupportedException();

        public CareerLocationEvaluation Evaluate(
            TownInfo town,
            CareerLocationRequirement requirement) =>
            throw new NotSupportedException();

        public LocationOpportunitySnapshot GetOpportunitySnapshot(IPerson person) =>
            throw new NotSupportedException();

        public LocationOpportunitySnapshot GetOpportunitySnapshot(TownInfo town) =>
            new(
                town,
                "Test Region",
                _regionTags,
                _townTags,
                "Test opportunity context");
    }
}
