using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Economy;
using Dynastia.Mechanics.Heirlooms;

namespace Dynastia.Core.Tests;

/// <summary>Small deterministic world using the real economy and heirloom stores.</summary>
internal sealed class RefactorFixture : IDisposable
{
    private static readonly Lazy<HeirloomCatalog> Catalog = new(() =>
        HeirloomCatalog.Load(new JsonGameDataService(RepositoryFiles.Path("data"))));
    private int _nextId;

    public RefactorFixture(params SequenceGameRandom.ExpectedCall[] calls)
    {
        Random = new SequenceGameRandom(calls);
        Economy = new StandardEconomyService(State, Family, new Locations(Town),
            new IncomeProviderRegistry(), new HouseholdIncomeProviderRegistry(),
            new Projections(), new Stats(), Random);
        Heirlooms = new StandardHeirloomService(State, Economy, Family, Random, Events, Catalog.Value);
    }

    public GameState State { get; } = new() { Year = 1900 };
    public SequenceGameRandom Random { get; }
    public FamilyStub Family { get; } = new();
    public GameEventBus Events { get; } = new();
    public StandardEconomyService Economy { get; }
    public StandardHeirloomService Heirlooms { get; }
    public TownInfo Town { get; } = new("Testowo", "Test County", 20, 52, 10000)
    { Id = "test-town", RegionId = "test-region" };

    public Guid NextId() => Guid.Parse($"00000000-0000-0000-0000-{++_nextId:D12}");

    public IPerson Person(int age = 30, bool alive = true, Sex sex = Sex.Male, string name = "Test")
    {
        var person = State.CreatePerson(name, "Family", age, NextId());
        person.BirthDate = new GameDate(State.Year - age, 1, 1);
        person.Tags.Add(alive ? "state.alive" : "state.dead");
        person.Tags.Add("family.bloodline");
        if (sex == Sex.Male) person.Tags.Add("lineage.male");
        Family.InitializePerson(person, sex);
        return person;
    }

    public HouseholdEconomyComponent Household(IPerson head, decimal wealth = 0m, IPerson? anchor = null)
    {
        var component = new HouseholdEconomyComponent
        {
            HouseholdId = NextId(), HeadId = head.Id, DynastyAnchorId = (anchor ?? head).Id,
            ResidenceTownId = Town.Id, Wealth = wealth, LegacyMembershipSeeded = true
        };
        component.MemberIds.Add(head.Id);
        head.Components.Set(component);
        return component;
    }

    public void Dispose() => Random.AssertComplete();

    private sealed class Projections : IHouseholdFinanceProjectionProviderRegistry
    {
        private readonly List<IHouseholdFinanceProjectionProvider> _providers = [];
        public IReadOnlyCollection<IHouseholdFinanceProjectionProvider> Providers => _providers;
        public void Register(IHouseholdFinanceProjectionProvider provider) => _providers.Add(provider);
    }

    private sealed class Locations(TownInfo town) : ILocationService
    {
        public LocationSnapshot GetLocation(IPerson person) => new(town, town, null);
        public TownInfo ChoosePropertyTown(IPerson head) => town;
        public IReadOnlyList<TownInfo> GetTowns() => [town];
        public TownInfo? FindTown(string id) => id == town.Id ? town : null;
        public void SetPersonHomeTown(IPerson person, TownInfo target) => throw new NotSupportedException();
        public void SetHouseholdHomeTown(IPerson head, TownInfo target) => throw new NotSupportedException();
    }

    private sealed class Stats : IStatsService
    {
        private static readonly IReadOnlyList<StatValue> Values =
            new[] { "immunity", "longevity", "fertility", "appeal", "strength", "intellect" }
                .Select(id => new StatValue(id, id, 3, string.Empty)).ToArray();
        public IReadOnlyList<StatValue> GetStats(IPerson person) => Values;
        public IReadOnlyList<StatValue> GetBaseStats(IPerson person) => Values;
        public void EnsureStats(IPerson person) { }
        public void SetStats(IPerson person, IReadOnlyDictionary<string, int> values) => throw new NotSupportedException();
        public bool TryIncreaseAcquiredStat(IPerson person, string statId) => throw new NotSupportedException();
    }

    internal sealed class FamilyStub : IFamilyService
    {
        private readonly Dictionary<Guid, Sex> _sex = [];
        private readonly Dictionary<Guid, IPerson?> _fathers = [], _mothers = [], _spouses = [];
        private readonly Dictionary<Guid, List<IPerson>> _children = [];
        public void InitializePerson(IPerson person, Sex sex, int? generation = null)
        {
            _sex[person.Id] = sex;
            person.Tags.Add(sex == Sex.Male ? "sex.male" : "sex.female");
        }
        public Sex GetSex(IPerson person) => _sex.GetValueOrDefault(person.Id, Sex.Male);
        public int? GetGeneration(IPerson person) => 1;
        public IPerson? GetFather(IPerson person) => _fathers.GetValueOrDefault(person.Id);
        public IPerson? GetMother(IPerson person) => _mothers.GetValueOrDefault(person.Id);
        public IPerson? GetSpouse(IPerson person) => _spouses.GetValueOrDefault(person.Id);
        public IReadOnlyList<IPerson> GetChildren(IPerson person) => _children.TryGetValue(person.Id, out var children) ? children : [];
        public void SetParents(IPerson child, IPerson? father, IPerson? mother)
        {
            foreach (var old in new[] { GetFather(child), GetMother(child) }.OfType<IPerson>())
                if (_children.TryGetValue(old.Id, out var previous)) previous.RemoveAll(p => p.Id == child.Id);
            _fathers[child.Id] = father;
            _mothers[child.Id] = mother;
            foreach (var parent in new[] { father, mother }.OfType<IPerson>().DistinctBy(p => p.Id))
            {
                if (!_children.TryGetValue(parent.Id, out var children)) _children[parent.Id] = children = [];
                children.Add(child);
            }
        }
        public void SetSpouses(IPerson first, IPerson second, int year)
        { _spouses[first.Id] = second; _spouses[second.Id] = first; }
        public void EndRelationship(IPerson first, IPerson second, int endYear, string endReason, bool clearFirst = true, bool clearSecond = true)
        { if (clearFirst) _spouses.Remove(first.Id); if (clearSecond) _spouses.Remove(second.Id); }
        public IReadOnlyList<RelationshipHistoryInfo> GetRelationshipHistory(IPerson person) => [];
        public void SetGeneratedFamilyBackground(IPerson person, GeneratedFamilyBackgroundInfo background) => throw new NotSupportedException();
        public GeneratedFamilyBackgroundInfo? GetGeneratedFamilyBackground(IPerson person) => null;
        public string FormatSurname(string surname, Sex sex) => surname;
        public string GetDisplayName(IPerson person) => $"{person.Name} {person.Surname}";
        public bool IsBloodline(IPerson person) => person.Tags.Has("family.bloodline");
        public bool IsMaleLineage(IPerson person) => person.Tags.Has("lineage.male");
    }
}
