using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Data;
using Dynastia.Core.Events;
using Dynastia.Core.Plugins;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Economy;
using Dynastia.Mechanics.Heirlooms;

namespace Dynastia.Core.Tests;

public sealed class HeirloomsBatch2Tests
{
    [Fact]
    public void EducationFiveCreatesExactlyOneHeirloomAcrossRepeatedSuccessEvents()
    {
        var fixture = CreateFixture();
        fixture.Education.SetEducationLevel(fixture.Head, 5);

        Publish(fixture, "education.success", fixture.Head, new() { ["level"] = "5" });
        Publish(fixture, "education.success", fixture.Head, new() { ["level"] = "5" });

        var academic = fixture.Heirlooms.GetHeirlooms(fixture.Head)
            .Where(item => item.OriginTriggerType == "education5")
            .ToList();
        Assert.Single(academic);
        Assert.StartsWith("education5:", academic[0].OriginTriggerId);
    }

    [Fact]
    public void CareerLevelFiveCreatesMappedHeirloomOnlyOnce()
    {
        var fixture = CreateFixture();

        Publish(fixture, "career.promotion", fixture.Head, new()
        {
            ["careerName"] = "Banking",
            ["jobLevel"] = "5"
        });
        Publish(fixture, "career.promotion", fixture.Head, new()
        {
            ["careerName"] = "Banking",
            ["jobLevel"] = "5"
        });

        var careerItems = fixture.Heirlooms.GetHeirlooms(fixture.Head)
            .Where(item => item.OriginTriggerType == "career5")
            .ToList();
        var item = Assert.Single(careerItems);
        Assert.Equal("career_finance_case", item.TemplateId);
    }

    [Fact]
    public void TwoDifferentMasteredCraftsCreateTwoMappedHeirloomsButEachCraftDedupes()
    {
        var fixture = CreateFixture();

        PublishCraftMaster(fixture, "metalworking", "Metalworking");
        PublishCraftMaster(fixture, "woodworking_carpentry", "Woodworking & Carpentry");
        PublishCraftMaster(fixture, "metalworking", "Metalworking");

        var craftItems = fixture.Heirlooms.GetHeirlooms(fixture.Head)
            .Where(item => item.OriginTriggerType == "craft5")
            .ToList();
        Assert.Equal(2, craftItems.Count);
        Assert.Contains(craftItems, item => item.TemplateId == "craft_master_hammer");
        Assert.Contains(craftItems, item => item.TemplateId == "craft_carved_chest");
    }

    [Fact]
    public void LargeFamilyDeathRequiresSixLivingChildren()
    {
        var six = CreateFixture();
        six.Head.Age = 80;
        six.Family.SetChildren(six.Head, CreateChildren(six, 6));
        Publish(six, "life.death", six.Head);
        Assert.Single(six.Heirlooms.GetHeirlooms(six.Head)
            .Where(item => item.OriginTriggerType == "large_family_death"));

        var five = CreateFixture();
        five.Head.Age = 80;
        five.Family.SetChildren(five.Head, CreateChildren(five, 5));
        Publish(five, "life.death", five.Head);
        Assert.Empty(five.Heirlooms.GetHeirlooms(five.Head)
            .Where(item => item.OriginTriggerType == "large_family_death"));
    }

    [Fact]
    public void LongevityDeathRequiresAgeOneHundred()
    {
        var hundred = CreateFixture();
        hundred.Head.Age = 100;
        Publish(hundred, "life.death", hundred.Head);
        Assert.Single(hundred.Heirlooms.GetHeirlooms(hundred.Head)
            .Where(item => item.OriginTriggerType == "longevity100"));

        var ninetyNine = CreateFixture();
        ninetyNine.Head.Age = 99;
        Publish(ninetyNine, "life.death", ninetyNine.Head);
        Assert.Empty(ninetyNine.Heirlooms.GetHeirlooms(ninetyNine.Head)
            .Where(item => item.OriginTriggerType == "longevity100"));
    }

    [Fact]
    public void WealthTierRollsOnlyOnceEvenAfterDroppingBelowAndCrossingAgain()
    {
        var random = new AlwaysSuccessRandom();
        var fixture = CreateFixture(random);
        var wealthSystem = Assert.Single(
            fixture.Systems.Systems.Where(system => system.Id == "heirlooms.wealth_milestones"));

        fixture.Economy.SetWealth(fixture.Head, 100000m);
        wealthSystem.Execute(fixture.State);
        Assert.Single(fixture.Heirlooms.GetHeirlooms(fixture.Head)
            .Where(item => item.OriginTriggerType == "wealth"));
        Assert.Equal(1, random.ChanceCalls);

        fixture.Economy.SetWealth(fixture.Head, 90000m);
        wealthSystem.Execute(fixture.State);
        fixture.Economy.SetWealth(fixture.Head, 100000m);
        wealthSystem.Execute(fixture.State);

        Assert.Single(fixture.Heirlooms.GetHeirlooms(fixture.Head)
            .Where(item => item.OriginTriggerType == "wealth"));
        Assert.Equal(1, random.ChanceCalls);
    }

    private static void PublishCraftMaster(Fixture fixture, string craftId, string craftName) =>
        Publish(fixture, "craft.became_master", fixture.Head, new()
        {
            ["craftId"] = craftId,
            ["craftName"] = craftName,
            ["level"] = "5"
        });

    private static void Publish(
        Fixture fixture,
        string type,
        IPerson subject,
        Dictionary<string, string>? data = null) =>
        fixture.Events.Publish(new GameEvent
        {
            Type = type,
            Year = fixture.State.Year,
            SubjectId = subject.Id,
            Data = data ?? new Dictionary<string, string>()
        });

    private static IPerson[] CreateChildren(Fixture fixture, int count)
    {
        var result = new List<IPerson>();
        for (var index = 0; index < count; index++)
        {
            var child = fixture.State.CreatePerson($"Child{index}", "Nowak", 10 + index);
            child.Tags.Add("state.alive");
            result.Add(child);
        }
        return result.ToArray();
    }

    private static Fixture CreateFixture(IGameRandom? suppliedRandom = null)
    {
        var random = suppliedRandom ?? new DeterministicRandom();
        var state = new GameState(random) { Year = 1900, StartYear = 1900 };
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
        var actions = new ActionRegistry(state, events, random, new ActionGuardRegistry());
        var systems = new YearSystemRegistry();
        var education = new TestEducationService();
        var career = new TestCareerService();
        var crafts = new TestCraftService(FindRepoRoot());
        var data = new JsonGameDataService(Path.Combine(FindRepoRoot(), "data"));

        var context = new GamePluginContext();
        context.AddService<IGameState>(state);
        context.AddService<IEconomyService>(economy);
        context.AddService<IFamilyService>(family);
        context.AddService<IWorkCapacityService>(new FullWorkCapacityService());
        context.AddService<IGameRandom>(random);
        context.AddService<IGameEventBus>(events);
        context.AddService<IActionRegistry>(actions);
        context.AddService<IGameDataService>(data);
        context.AddService<IYearSystemRegistry>(systems);
        context.AddService<IEducationService>(education);
        context.AddService<ICareerService>(career);
        context.AddService<ICraftService>(crafts);
        new HeirloomsPlugin().Initialize(context);

        return new Fixture(
            state,
            head,
            family,
            economy,
            events,
            systems,
            education,
            career,
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
        TestFamilyService Family,
        StandardEconomyService Economy,
        GameEventBus Events,
        YearSystemRegistry Systems,
        TestEducationService Education,
        TestCareerService Career,
        IHeirloomService Heirlooms);

    private class DeterministicRandom : IGameRandom
    {
        private int _value;
        public virtual int NextInt(int minInclusive, int maxInclusive)
        {
            var span = maxInclusive - minInclusive + 1;
            return minInclusive + Math.Abs(_value++ % span);
        }
        public virtual double NextDouble() => 0.5;
        public virtual bool Chance(double probability) => probability >= 1.0;
    }

    private sealed class AlwaysSuccessRandom : DeterministicRandom
    {
        public int ChanceCalls { get; private set; }
        public override bool Chance(double probability)
        {
            ChanceCalls++;
            return true;
        }
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
        public void SetChildren(IPerson parent, params IPerson[] children) =>
            _children[parent.Id] = children.ToList();
        public void InitializePerson(IPerson person, Sex sex, int? generation = null) { }
        public Sex GetSex(IPerson person) => Sex.Male;
        public int? GetGeneration(IPerson person) => 1;
        public IPerson? GetFather(IPerson person) => null;
        public IPerson? GetMother(IPerson person) => null;
        public IPerson? GetSpouse(IPerson person) => null;
        public IReadOnlyList<IPerson> GetChildren(IPerson person) =>
            _children.TryGetValue(person.Id, out var children) ? children : [];
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

    private sealed class TestEducationService : IEducationService
    {
        private readonly Dictionary<Guid, int> _levels = [];
        public void EnsureEducation(IPerson person) { }
        public int GetEducationLevel(IPerson person) => _levels.GetValueOrDefault(person.Id);
        public void SetEducationLevel(IPerson person, int level) => _levels[person.Id] = level;
        public void IncreaseEducation(IPerson person, int amount = 1) => _levels[person.Id] = GetEducationLevel(person) + amount;
        public double GetPaidEducationSuccessChance(IPerson person) => 1;
        public int GetLocalEducationCeiling(IPerson person, int year) => 5;
        public int GetLocalEducationCeiling(TownInfo town, int year) => 5;
        public EducationGenerationRange GetGeneratedAdultRange(int year) => new(0, 5);
        public EducationGenerationRange GetGeneratedAdultRange(int year, TownInfo town) => new(0, 5);
    }

    private sealed class TestCareerService : ICareerService
    {
        private CareerSnapshot _snapshot = new(5, "Magnate", 5, "Thriving", 0, 0, false, "banking", "Banking");
        public void EnsureCareer(IPerson person) { }
        public CareerSnapshot GetCareer(IPerson person) => _snapshot;
        public void InitializeCareer(IPerson person, int jobLevel, int jobSatisfaction) { }
        public void SetJobLevel(IPerson person, int jobLevel) => _snapshot = _snapshot with { JobLevel = jobLevel };
        public void ChangeJobSatisfaction(IPerson person, int amount) { }
        public string GetStatusLabel(string statusId) => statusId;
        public void Retire(IPerson person) { }
        public decimal GetAnnualIncome(IPerson person) => 0;
        public bool IsEmployed(IPerson person) => _snapshot.JobLevel > 0;
        public decimal GetLevelOneSalary(string careerId) => 500;
        public IReadOnlyList<JobOpportunityInfo> GetJobOpportunities(IPerson person, int count = 5) => [];
        public JobApplicationResult ApplyForJob(IPerson person, string careerId, int jobLevel) => throw new NotSupportedException();
        public void AssignCareer(IPerson person, string? careerId, int jobLevel, int jobSatisfaction) { }
        public GeneratedCareerProfile GenerateCandidateCareer(GeneratedCareerContext context) => throw new NotSupportedException();
        public string? GetCareerFamily(IPerson person) => "finance";
        public IReadOnlyCollection<string> GetKnownCareerFamilies() => KnownCareerFamilies;
        public bool TryFindBetterJob(IPerson person) => false;
        public bool TryFindEmployment(IPerson person, double chanceBonus = 0) => false;
        public bool RelocateEmployment(IPerson person) => false;
        public IReadOnlyDictionary<string, int> GetExperienceYearsByCareer(IPerson person) => new Dictionary<string, int>();

        private static readonly string[] KnownCareerFamilies =
        [
            "agriculture", "business_services", "chemical", "commerce", "construction", "education",
            "engineering", "extraction", "finance", "food", "forestry_wood", "healthcare", "heavy_industry",
            "hospitality", "legal", "manufacturing", "maritime", "media_creative", "media_print", "military",
            "personal_services", "property", "public_service", "religion", "science_health", "security",
            "skilled_trades", "technology", "textiles_apparel", "transport", "utilities"
        ];
    }

    private sealed class TestCraftService : ICraftService
    {
        public TestCraftService(string repoRoot)
        {
            var legacyCraftIds = File.ReadLines(Path.Combine(repoRoot, "data", "Heirlooms", "craft_master_heirlooms.csv"))
                .Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.TrimStart('\uFEFF').Split(',')[0]);
            var artisticCraftIds = File.ReadLines(Path.Combine(repoRoot, "data", "Crafts", "artistic_crafts_append.csv"))
                .Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.TrimStart('\uFEFF').Split(',')[0]);

            Catalog = legacyCraftIds
                .Concat(artisticCraftIds)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => new CraftInfo(
                    id, id, 1700, null, 10, 1, 500, "intellect", null, "Universal",
                    SettlementClass.SmallTown, [], [], "🛠️", id, [], []))
                .ToList();
        }
        public IReadOnlyList<CraftInfo> Catalog { get; }
        public CraftSnapshot GetSnapshot(IPerson person) => throw new NotSupportedException();
        public IReadOnlyList<CraftInfo> GetKnownCrafts(IPerson person) => [];
        public bool KnowsCraft(IPerson person, string craftId) => false;
        public bool IsSelfEmployed(IPerson person) => false;
        public CraftInfo? GetActiveCraft(IPerson person) => null;
        public CraftProgressSnapshot? GetProgress(IPerson person, string craftId) => null;
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
        public CraftCareerExperience GetCareerExperience(IPerson person, string careerId) => throw new NotSupportedException();
        public decimal GetExpectedAnnualIncome(IPerson person) => 0;
    }
}
