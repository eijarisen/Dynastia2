using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Data;
using Dynastia.Core.Events;
using Dynastia.Core.Plugins;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Economy;
using Dynastia.Mechanics.Heirlooms;

namespace Dynastia.Core.Tests;

public sealed class HeirloomsBatch3Tests
{
    [Fact]
    public void HistoricalHeirloomRequiresDirectHouseholdImpactAndRollsOnlyOncePerHouseholdEvent()
    {
        var random = new ScriptedRandom(true);
        var fixture = CreateFixture(random);

        Publish(fixture, "historical.milestone", new()
        {
            ["eventId"] = "first_partition"
        });
        Assert.Empty(fixture.Heirlooms.GetHeirlooms(fixture.Head));

        Publish(fixture, "historical.household_impact", new()
        {
            ["eventId"] = "first_partition"
        });
        Publish(fixture, "historical.relocation", new()
        {
            ["eventId"] = "first_partition"
        });

        var item = Assert.Single(fixture.Heirlooms.GetHeirlooms(fixture.Head));
        Assert.Equal("historical", item.OriginTriggerType);
        Assert.Equal("historical_commonwealth_document", item.TemplateId);
        Assert.Equal(1, random.ChanceCalls);
    }

    [Fact]
    public void SelectedRareEventCanCreateMappedHeirloomAndDuplicatePublicationDoesNotDuplicateIt()
    {
        var fixture = CreateFixture(new ScriptedRandom(true));

        Publish(fixture, "rare.professional_recognition", new()
        {
            ["displayName"] = "Professional Recognition"
        });
        Publish(fixture, "rare.professional_recognition", new()
        {
            ["displayName"] = "Professional Recognition"
        });

        var item = Assert.Single(fixture.Heirlooms.GetHeirlooms(fixture.Head));
        Assert.Equal("rare_professional_plaque", item.TemplateId);
        Assert.Equal("rare_event", item.OriginTriggerType);
    }

    [Fact]
    public void CrimeHeirloomRequiresSuccessfulUncaughtMappedCrimeAndIsMarkedStolen()
    {
        var fixture = CreateFixture(new ScriptedRandom(true));

        Publish(fixture, "justice.crime", new()
        {
            ["crimeId"] = "burglary",
            ["crime"] = "burglary",
            ["success"] = "true",
            ["caught"] = "true"
        });
        Publish(fixture, "justice.crime_uncaught", new()
        {
            ["crimeId"] = "burglary",
            ["crime"] = "burglary",
            ["success"] = "false",
            ["caught"] = "false"
        });
        Assert.Empty(fixture.Heirlooms.GetHeirlooms(fixture.Head));

        fixture.State.Year++;
        Publish(fixture, "justice.crime_uncaught", new()
        {
            ["crimeId"] = "burglary",
            ["crime"] = "burglary",
            ["success"] = "true",
            ["caught"] = "false"
        });

        var item = Assert.Single(fixture.Heirlooms.GetHeirlooms(fixture.Head));
        Assert.Equal("crime", item.OriginTriggerType);
        Assert.True(item.IsStolen);
        Assert.StartsWith("crime_", item.TemplateId);
    }

    [Fact]
    public void HobbyKeepsakeIsRareAnnualRollAndCreatesAtMostOnePerPersonAndHobby()
    {
        var random = new ScriptedRandom(false, true, true);
        var fixture = CreateFixture(random, "reading");
        fixture.Head.Age = 40;
        var system = Assert.Single(
            fixture.Systems.Systems, system => system.Id == "heirlooms.hobby_keepsakes");

        system.Execute(fixture.State);
        Assert.Empty(fixture.Heirlooms.GetHeirlooms(fixture.Head));

        fixture.State.Year++;
        system.Execute(fixture.State);
        var item = Assert.Single(fixture.Heirlooms.GetHeirlooms(fixture.Head));
        Assert.Equal("hobby_rare_book", item.TemplateId);
        Assert.Equal("hobby", item.OriginTriggerType);

        fixture.State.Year++;
        system.Execute(fixture.State);
        Assert.Single(fixture.Heirlooms.GetHeirlooms(fixture.Head));
        Assert.Equal(2, random.ChanceCalls);
    }

    private static void Publish(
        Fixture fixture,
        string type,
        Dictionary<string, string> data) =>
        fixture.Events.Publish(new GameEvent
        {
            Type = type,
            Year = fixture.State.Year,
            SubjectId = fixture.Head.Id,
            Data = data
        });

    private static Fixture CreateFixture(
        IGameRandom random,
        params string[] hobbyIds)
    {
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
        var hobbies = new TestHobbyService();
        hobbies.SetHobbies(head, hobbyIds);
        var data = new JsonGameDataService(Path.Combine(RepositoryFiles.Root, "data"));

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
        context.AddService<IHobbyService>(hobbies);
        new HeirloomsPlugin().Initialize(context);

        return new Fixture(
            state,
            head,
            economy,
            events,
            systems,
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

    private sealed record Fixture(
        GameState State,
        IPerson Head,
        StandardEconomyService Economy,
        GameEventBus Events,
        YearSystemRegistry Systems,
        IHeirloomService Heirlooms);

    private sealed class ScriptedRandom : IGameRandom
    {
        private readonly Queue<bool> _chanceResults;
        private int _value;

        public ScriptedRandom(params bool[] chanceResults) =>
            _chanceResults = new Queue<bool>(chanceResults);

        public int ChanceCalls { get; private set; }

        public int NextInt(int minInclusive, int maxInclusive)
        {
            var span = maxInclusive - minInclusive + 1;
            return minInclusive + Math.Abs(_value++ % span);
        }

        public double NextDouble() => 0.5;

        public bool Chance(double probability)
        {
            ChanceCalls++;
            return _chanceResults.Count > 0
                ? _chanceResults.Dequeue()
                : probability >= 1.0;
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

    private sealed class TestHobbyService : IHobbyService
    {
        private readonly Dictionary<Guid, IReadOnlyList<HobbyInfo>> _hobbies = [];

        public HobbyPersonSnapshot GetHobbies(IPerson person) =>
            new(2, _hobbies.GetValueOrDefault(person.Id, []));

        public IReadOnlyList<HobbyInfo> GenerateCandidateHobbies(
            Guid candidateId,
            Sex sex,
            int age,
            int year,
            string temperament,
            SettlementClass settlementClass,
            int strength,
            int intellect,
            int appeal) => [];

        public void SetHobbies(IPerson person, IReadOnlyCollection<string> hobbyIds) =>
            _hobbies[person.Id] = hobbyIds
                .Select(id => new HobbyInfo(id, DisplayName(id), "🎨"))
                .ToList();

        public void ReconcileAll() { }
        public void ReconcileAfterLoad() { }

        private static string DisplayName(string id) => id switch
        {
            "reading" => "Reading",
            "playing_music" => "Playing Music",
            _ => id.Replace('_', ' ')
        };
    }
}
