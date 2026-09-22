using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Crafts;
using Dynastia.Mechanics.Economy;
using Dynastia.Mechanics.Heirlooms;
using Dynastia.Mechanics.Status;

namespace Dynastia.Core.Tests;

public sealed class LocalSocietyArtisticWorksBatch11Tests
{
    [Fact]
    public void SuppliedProductionRulesUseExpectedChanceAndValueBands()
    {
        var data = RepositoryData();
        var heirlooms = HeirloomCatalog.Load(data);
        var crafts = CraftCatalog.Load(data);
        var artistic = ArtisticWorkCatalog.Load(data, heirlooms, crafts.All);

        AssertRule(artistic, 1, 0.01, 500, 1000);
        AssertRule(artistic, 2, 0.02, 1000, 2000);
        AssertRule(artistic, 3, 0.04, 2500, 5000);
        AssertRule(artistic, 4, 0.07, 7000, 14000);
        AssertRule(artistic, 5, 0.12, 18000, 40000);
        Assert.Equal(70, artistic.RoyaltyAfterDeathYears);

        foreach (var craftId in new[] { "musician", "painter", "writer", "sculptor" })
        {
            Assert.True(artistic.IsArtisticCraft(craftId));
            for (var mastery = 1; mastery <= 5; mastery++)
                Assert.Equal(craftId, artistic.ChooseTemplate(craftId, mastery, new DeterministicRandom()).CraftId);
        }
    }

    [Fact]
    public void MasterGuaranteeCreatesOneWorkAndSuppressesSameYearRandomRoll()
    {
        var fixture = CreateFixture();
        var data = RepositoryData();
        var craftCatalog = CraftCatalog.Load(data);
        var artistic = ArtisticWorkCatalog.Load(data, fixture.Catalog, craftCatalog.All);
        var writer = craftCatalog.Find("writer")!;
        var crafts = new MasterWriterCraftService(writer);
        var random = new DeterministicRandom(chanceResult: false);
        var system = new ArtisticWorkYearSystem(
            fixture.Family,
            crafts,
            fixture.Heirlooms,
            new FullWorkCapacityService(),
            random,
            fixture.Events,
            artistic);

        system.Execute(fixture.State);
        system.Execute(fixture.State);

        var created = Assert.Single(fixture.Heirlooms.GetHeirlooms(fixture.Head));
        Assert.InRange(created.AppraisedValue, 18000m, 40000m);
        Assert.Equal(fixture.Head.Id, created.RoyaltyAuthorId);
        Assert.Equal(0.01m, created.RoyaltyAnnualRate);
        Assert.Equal(0, random.ChanceCalls);
        Assert.Single(fixture.Events.AllEvents.Where(item => item.Type == "artistic.work_created"));

        fixture.State.Year++;
        system.Execute(fixture.State);
        Assert.Equal(1, random.ChanceCalls);
        Assert.Single(fixture.Heirlooms.GetHeirlooms(fixture.Head));
    }

    [Fact]
    public void ZeroWorkCapacityDefersMasterGuaranteeWithoutConsumingEvaluation()
    {
        var fixture = CreateFixture();
        var data = RepositoryData();
        var craftCatalog = CraftCatalog.Load(data);
        var artistic = ArtisticWorkCatalog.Load(data, fixture.Catalog, craftCatalog.All);
        var writer = craftCatalog.Find("writer")!;
        var crafts = new MasterWriterCraftService(writer);
        var random = new DeterministicRandom(chanceResult: false);

        new ArtisticWorkYearSystem(
            fixture.Family,
            crafts,
            fixture.Heirlooms,
            new NoWorkCapacityService(),
            random,
            fixture.Events,
            artistic).Execute(fixture.State);

        Assert.Empty(fixture.Heirlooms.GetHeirlooms(fixture.Head));
        Assert.Null(fixture.Head.Components.Get<ArtisticWorkPersonComponent>());
        Assert.Equal(0, random.ChanceCalls);

        new ArtisticWorkYearSystem(
            fixture.Family,
            crafts,
            fixture.Heirlooms,
            new FullWorkCapacityService(),
            random,
            fixture.Events,
            artistic).Execute(fixture.State);

        Assert.Single(fixture.Heirlooms.GetHeirlooms(fixture.Head));
        Assert.Equal(0, random.ChanceCalls);
    }

    [Fact]
    public void OrdinaryArtChanceScalesWithSharedProductiveEffort()
    {
        var fixture = CreateFixture();
        var data = RepositoryData();
        var craftCatalog = CraftCatalog.Load(data);
        var artistic = ArtisticWorkCatalog.Load(data, fixture.Catalog, craftCatalog.All);
        var writer = craftCatalog.Find("writer")!;
        var crafts = new MasterWriterCraftService(writer, masteryLevel: 4);
        var random = new DeterministicRandom(chanceResult: false);
        var system = new ArtisticWorkYearSystem(
            fixture.Family,
            crafts,
            fixture.Heirlooms,
            new HalfWorkCapacityService(),
            random,
            fixture.Events,
            artistic);

        system.Execute(fixture.State);

        Assert.Equal(1, random.ChanceCalls);
        Assert.Equal(0.035, random.LastChanceProbability, 10);
        Assert.Empty(fixture.Heirlooms.GetHeirlooms(fixture.Head));
    }

    [Fact]
    public void ArtisticCreationUsesDirectValueOverrideAndPersistsRoyaltyMetadata()
    {
        var fixture = CreateFixture();
        var item = fixture.Heirlooms.Create(
            fixture.Head,
            new HeirloomCreationRequest(
                "art_write_3_published_book",
                fixture.Head.Id,
                "artistic_work",
                "test",
                "Test work.",
                AppraisedValueOverride: 4321m,
                RoyaltyAuthorId: fixture.Head.Id,
                RoyaltyAnnualRate: 0.005m));

        Assert.Equal(4321m, item.AppraisedValue);
        Assert.Equal(fixture.Head.Id, item.RoyaltyAuthorId);
        Assert.Equal(0.005m, item.RoyaltyAnnualRate);

        var listed = Assert.Single(fixture.Heirlooms.GetHeirlooms(fixture.Head));
        Assert.Equal(item.RoyaltyAuthorId, listed.RoyaltyAuthorId);
        Assert.Equal(item.RoyaltyAnnualRate, listed.RoyaltyAnnualRate);
    }

    [Fact]
    public void RoyaltiesFollowOwnedHeirloomPendingPaysNothingAndSaleStopsIncome()
    {
        var fixture = CreateFixture();
        var child = fixture.State.CreatePerson("Anna", "Nowak", 30);
        child.Tags.Add("state.alive");
        AttachHousehold(child, fixture.Town, 0m);

        var item = fixture.Heirlooms.Create(
            fixture.Head,
            new HeirloomCreationRequest(
                "art_write_5_masterwork_book",
                fixture.Head.Id,
                "artistic_work",
                "test",
                "Test work.",
                AppraisedValueOverride: 10000m,
                RoyaltyAuthorId: fixture.Head.Id,
                RoyaltyAnnualRate: 0.01m));
        var royalties = new ArtisticRoyaltyIncomeProvider(fixture.State, fixture.Heirlooms, 70);

        Assert.Equal(100m, royalties.GetAnnualIncome(fixture.Head));
        Assert.Equal(100m, royalties.GetExpectedAnnualIncome(fixture.Head));

        var moved = fixture.Heirlooms.Take(fixture.Head, item.Id)!;
        fixture.Heirlooms.AddPending(child, moved, fixture.State.Year, "inherited_pending");
        Assert.Equal(0m, royalties.GetAnnualIncome(fixture.Head));
        Assert.Equal(0m, royalties.GetAnnualIncome(child));

        var pending = Assert.Single(fixture.Heirlooms.TakePending(child));
        fixture.Heirlooms.AddExisting(child, pending, fixture.State.Year, child.Id, "inherited");
        Assert.Equal(100m, royalties.GetAnnualIncome(child));
        Assert.Equal(100m, royalties.GetExpectedAnnualIncome(child));

        Assert.NotNull(fixture.Heirlooms.Take(child, item.Id));
        Assert.Equal(0m, royalties.GetAnnualIncome(child));
    }

    [Fact]
    public void RoyaltiesContinueThroughDeathYearPlusSeventyInclusive()
    {
        var fixture = CreateFixture();
        fixture.Heirlooms.Create(
            fixture.Head,
            new HeirloomCreationRequest(
                "art_music_5_masterwork_composition",
                fixture.Head.Id,
                "artistic_work",
                "test",
                "Test composition.",
                AppraisedValueOverride: 20000m,
                RoyaltyAuthorId: fixture.Head.Id,
                RoyaltyAnnualRate: 0.01m));
        var royalties = new ArtisticRoyaltyIncomeProvider(fixture.State, fixture.Heirlooms, 70);

        fixture.Head.Tags.Remove("state.alive");
        fixture.Head.DeathDate = new GameDate(1900, 6, 1);

        fixture.State.Year = 1970;
        Assert.Equal(200m, royalties.GetAnnualIncome(fixture.Head));
        Assert.Equal(200m, royalties.GetExpectedAnnualIncome(fixture.Head));

        fixture.State.Year = 1971;
        Assert.Equal(0m, royalties.GetAnnualIncome(fixture.Head));
        Assert.Equal(0m, royalties.GetExpectedAnnualIncome(fixture.Head));
    }

    [Fact]
    public void ArtisticStatusGainsCapPerPersonAndCraft()
    {
        var rules = ArtisticStatusRules.Load(RepositoryData());
        var component = new StatusComponent();

        var renown = 0.0;
        var reputation = 0.0;
        for (var index = 0; index < 10; index++)
        {
            var delta = rules.Consume(component, "writer", 5);
            renown += delta.Renown;
            reputation += delta.Reputation;
        }

        Assert.Equal(12, renown, 10);
        Assert.Equal(3, reputation, 10);
        Assert.Equal(12, component.ArtisticRenownByCraft["writer"], 10);
        Assert.Equal(3, component.ArtisticReputationByCraft["writer"], 10);

        var painter = rules.Consume(component, "painter", 5);
        Assert.Equal(4, painter.Renown, 10);
        Assert.Equal(1, painter.Reputation, 10);
    }

    private static void AssertRule(
        ArtisticWorkCatalog catalog,
        int mastery,
        double chance,
        int minimum,
        int maximum)
    {
        var rule = catalog.GetProductionRule(mastery);
        Assert.Equal(chance, rule.AnnualProductionChance, 10);
        Assert.Equal(minimum, rule.MinimumValue);
        Assert.Equal(maximum, rule.MaximumValue);
    }

    private static Fixture CreateFixture()
    {
        var random = new DeterministicRandom();
        var state = new GameState(random)
        {
            Year = 1900,
            StartYear = 1900
        };
        var head = state.CreatePerson("Jan", "Nowak", 40);
        head.Tags.Add("state.alive");
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
        AttachHousehold(head, town, 0m);
        var events = new GameEventBus();
        var catalog = HeirloomCatalog.Load(RepositoryData());
        var service = new StandardHeirloomService(
            state,
            economy,
            family,
            random,
            events,
            catalog);
        return new Fixture(state, head, town, family, events, catalog, service);
    }

    private static void AttachHousehold(IPerson head, TownInfo town, decimal wealth)
    {
        var household = new HouseholdEconomyComponent
        {
            HouseholdId = Guid.NewGuid(),
            HeadId = head.Id,
            DynastyAnchorId = head.Id,
            ResidenceTownId = town.Id,
            Wealth = wealth,
            LegacyMembershipSeeded = true
        };
        household.MemberIds.Add(head.Id);
        head.Components.Set(household);
    }

    private static IGameDataService RepositoryData()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "data");
            if (File.Exists(Path.Combine(path, "Heirlooms", "artistic_work_rules.json")))
                return new JsonGameDataService(path);
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository data directory.");
    }

    private sealed record Fixture(
        GameState State,
        IPerson Head,
        TownInfo Town,
        TestFamilyService Family,
        GameEventBus Events,
        HeirloomCatalog Catalog,
        StandardHeirloomService Heirlooms);

    private sealed class FullWorkCapacityService : IWorkCapacityService
    {
        public WorkCapacitySnapshot GetWorkCapacity(IPerson person) =>
            new(1.0, true);
    }

    private sealed class HalfWorkCapacityService : IWorkCapacityService
    {
        public WorkCapacitySnapshot GetWorkCapacity(IPerson person) =>
            new(0.5, true);
    }

    private sealed class NoWorkCapacityService : IWorkCapacityService
    {
        public WorkCapacitySnapshot GetWorkCapacity(IPerson person) =>
            new(0, false);
    }

    private sealed class DeterministicRandom : IGameRandom
    {
        private readonly bool _chanceResult;
        public DeterministicRandom(bool chanceResult = false) => _chanceResult = chanceResult;
        public int ChanceCalls { get; private set; }
        public double LastChanceProbability { get; private set; }
        public int NextInt(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => 0.5;
        public bool Chance(double probability)
        {
            ChanceCalls++;
            LastChanceProbability = probability;
            return _chanceResult;
        }
    }

    private sealed class MasterWriterCraftService : ICraftService
    {
        private readonly CraftInfo _writer;
        private readonly int _masteryLevel;
        public MasterWriterCraftService(CraftInfo writer, int masteryLevel = 5)
        {
            _writer = writer;
            _masteryLevel = masteryLevel;
        }
        public IReadOnlyList<CraftInfo> Catalog => [_writer];
        public CraftSnapshot GetSnapshot(IPerson person) => new(
            [_writer], _writer.Id, _writer.SelfEmploymentTitle, _writer.Emoji,
            new Dictionary<string, int> { [_writer.Id] = 10 }, 0m, 0m, 0);
        public IReadOnlyList<CraftInfo> GetKnownCrafts(IPerson person) => [_writer];
        public bool KnowsCraft(IPerson person, string craftId) => craftId == _writer.Id;
        public bool IsSelfEmployed(IPerson person) => true;
        public CraftInfo? GetActiveCraft(IPerson person) => _writer;
        public CraftProgressSnapshot? GetProgress(IPerson person, string craftId) => new(
            _writer.Id,
            _writer.Name,
            _masteryLevel,
            _masteryLevel >= 5 ? "Master" : "Expert",
            100,
            100,
            0,
            10,
            10,
            0,
            0m,
            null,
            null);
        public IReadOnlyList<CraftEducationOption> GetEducationOptions(IPerson person) => [];
        public CraftEducationResult StudyCraft(IPerson person, string craftId) => throw new NotSupportedException();
        public bool CanLearnCraft(IPerson person, string craftId) => false;
        public double GetLearningWeight(IPerson person, string craftId) => 0;
        public bool LearnCraft(IPerson person, string craftId) => false;
        public void SetCrafts(IPerson person, IEnumerable<string> craftIds) { }
        public IReadOnlyList<string> GenerateCandidateCraftIds(string deterministicKey, string? formalCareerId, int year, Sex sex, int age, int strength, int intellect, string temperament, TownInfo town) => [];
        public bool StartOccupation(IPerson person, string craftId) => false;
        public bool EndOccupation(IPerson person, string reason = "ended") => false;
        public double GetApplicationBonus(IPerson person, string careerId) => 0;
        public CraftCareerExperience GetCareerExperience(IPerson person, string careerId) => new(0, 0);
        public decimal GetExpectedAnnualIncome(IPerson person) => 0m;
    }

    private sealed class TestProjectionRegistry : IHouseholdFinanceProjectionProviderRegistry
    {
        public IReadOnlyCollection<IHouseholdFinanceProjectionProvider> Providers => [];
        public void Register(IHouseholdFinanceProjectionProvider provider) { }
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
        public void InitializePerson(IPerson person, Sex sex, int? generation = null) { }
        public Sex GetSex(IPerson person) => Sex.Male;
        public int? GetGeneration(IPerson person) => 1;
        public IPerson? GetFather(IPerson person) => null;
        public IPerson? GetMother(IPerson person) => null;
        public IPerson? GetSpouse(IPerson person) => null;
        public IReadOnlyList<IPerson> GetChildren(IPerson person) => [];
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
