using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Economy;
using Dynastia.Mechanics.Locations;

namespace Dynastia.Core.Tests;

public sealed class HistoricalTownCatalogBatch2Tests
{
    private const string Warszawa = "p_a0efc8373a9b279b";
    private const string Lwow = "p_936305d7bd070389";
    private const string Katowice = "p_a8c3eadb5c8dca2e";
    private const string Krakow = "p_b2feaac170f8446f";

    [Fact]
    public void LocationServiceUsesCurrentHistoricalAvailabilityAndYearResolvedTownData()
    {
        var state = new GameState { Year = 1939, StartYear = 1939 };
        var catalog = LoadCatalog();
        var service = CreateLocationService(state, catalog);

        Assert.Contains(service.GetTowns(), town => town.Id == Lwow);

        state.Year = 1950;

        Assert.DoesNotContain(service.GetTowns(), town => town.Id == Lwow);
        var lwow1950 = service.FindTown(Lwow);
        Assert.NotNull(lwow1950);
        Assert.Equal(Lwow, lwow1950!.Id);
        Assert.False(lwow1950.IsDestinationAvailable);

        state.Year = 1900;
        var warsaw1900 = service.FindTown(Warszawa)!;
        Assert.Equal(686000, warsaw1900.Population);

        state.Year = 2026;
        var warsaw2026 = service.FindTown(Warszawa)!;
        Assert.NotEqual(warsaw1900.Population, warsaw2026.Population);
    }

    [Fact]
    public void LocationComponentStoresPermanentIdsAndResolvesBirthHomeAndDeathAtTheirOwnYears()
    {
        var state = new GameState { Year = 1956, StartYear = 1950 };
        var catalog = LoadCatalog();
        var service = CreateLocationService(state, catalog);

        var person = state.CreatePerson("Jan", "Nowak", 2);
        person.BirthDate = new GameDate(1954, 1, 1);
        person.DeathDate = new GameDate(1955, 1, 1);
        person.Components.Set(new LocationComponent
        {
            BirthplaceId = Katowice,
            HomeTownId = Katowice,
            DeathTownId = Katowice
        });

        var location = service.GetLocation(person);

        Assert.Equal("Stalinogród", location.Birthplace.Town);
        Assert.Equal("Katowice", location.HomeTown.Town);
        Assert.Equal("Stalinogród", location.DeathTown!.Town);
        Assert.Equal(Katowice, location.HomeTown.Id);

        var persisted = person.Components.Get<LocationComponent>()!;
        Assert.Equal(Katowice, persisted.BirthplaceId);
        Assert.Equal(Katowice, persisted.HomeTownId);
        Assert.Equal(Katowice, persisted.DeathTownId);

        Assert.Null(typeof(LocationComponent).GetProperty("Birthplace"));
        Assert.Null(typeof(LocationComponent).GetProperty("HomeTown"));
        Assert.Null(typeof(LocationComponent).GetProperty("DeathTown"));
    }

    [Fact]
    public void ExistingHouseholdsKeepPermanentPlaceIdsWhenTownLeavesDestinationPool()
    {
        var state = new GameState { Year = 1950, StartYear = 1930 };
        var catalog = LoadCatalog();
        var service = CreateLocationService(
            state,
            catalog,
            new ZeroRandom());

        var head = state.CreatePerson("Jan", "Nowak", 40);
        head.BirthDate = new GameDate(1910, 1, 1);
        head.Components.Set(new LocationComponent
        {
            BirthplaceId = Lwow,
            HomeTownId = Lwow
        });

        var home = service.GetLocation(head).HomeTown;
        Assert.Equal(Lwow, home.Id);
        Assert.False(home.IsDestinationAvailable);

        var newPropertyTown = service.ChoosePropertyTown(head);
        Assert.True(newPropertyTown.IsDestinationAvailable);
        Assert.Contains(service.GetTowns(), town => town.Id == newPropertyTown.Id);

        Assert.Equal(
            Lwow,
            head.Components.Get<LocationComponent>()!.HomeTownId);
    }

    [Fact]
    public void HeterosexualMarriageMovesTheWifeToTheHusbandsHomeTown()
    {
        var state = new GameState { Year = 1900, StartYear = 1900 };
        var catalog = LoadCatalog();
        var events = new GameEventBus();
        var family = new SexAwareFamilyService();
        var service = new StandardLocationService(
            state,
            family,
            catalog,
            new ZeroRandom(),
            events);

        var woman = state.CreatePerson("Anna", "Kowalska", 22);
        woman.BirthDate = new GameDate(1878, 1, 1);
        woman.Components.Set(new LocationComponent
        {
            BirthplaceId = Krakow,
            HomeTownId = Krakow
        });
        family.SetSex(woman, Sex.Female);

        var man = state.CreatePerson("Jan", "Nowak", 25);
        man.BirthDate = new GameDate(1875, 1, 1);
        man.Components.Set(new LocationComponent
        {
            BirthplaceId = Warszawa,
            HomeTownId = Warszawa
        });
        family.SetSex(man, Sex.Male);

        events.Publish(new GameEvent
        {
            Type = "relationship.married",
            Year = 1900,
            SubjectId = woman.Id,
            RelatedPersonIds = [man.Id]
        });

        Assert.Equal(Warszawa, service.GetLocation(man).HomeTown.Id);
        Assert.Equal(Warszawa, service.GetLocation(woman).HomeTown.Id);
        Assert.Equal(Krakow, service.GetLocation(woman).Birthplace.Id);
    }

    [Fact]
    public void HousePropertyPersistenceUsesTownIdOnly()
    {
        var property = new HousePropertyState
        {
            Id = Guid.NewGuid(),
            TownId = Katowice
        };

        Assert.Equal(Katowice, property.TownId);
        Assert.Null(typeof(HousePropertyState).GetProperty("Town"));
    }

    [Fact]
    public void OpportunityFilesUsePermanentPlacesAndCoverAllHistoricalRegions()
    {
        var root = RepositoryFiles.Root;
        var catalog = LoadCatalog();

        var townPath = Path.Combine(root, "data", "Towns", "town_opportunities.csv");
        var townLines = File.ReadAllLines(townPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        Assert.Equal(134, townLines.Length);

        foreach (var line in townLines.Skip(1))
        {
            var townId = line.Split(',')[0].TrimStart('\uFEFF').Trim();
            Assert.StartsWith("p_", townId);
            Assert.NotNull(catalog.GetTown(townId, catalog.MinYear));
        }

        var regionPath = Path.Combine(root, "data", "Towns", "region_opportunities.csv");
        var configuredRegions = File.ReadAllLines(regionPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Skip(1)
            .Select(line => line.Split(',')[0].TrimStart('\uFEFF').Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(catalog.Regions.Count, configuredRegions.Count);
        Assert.All(catalog.Regions.Keys, region => Assert.Contains(region, configuredRegions));

        Assert.False(File.Exists(Path.Combine(root, "data", "Towns", "towns.csv")));
    }

    [Fact]
    public void LocalOpportunityServiceAcceptsHistoricalPermanentIdsAndEasternRegions()
    {
        var root = RepositoryFiles.Root;
        var data = new JsonGameDataService(Path.Combine(root, "data"));
        var catalog = HistoricalTownCatalog.Load(data);
        var state = new GameState { Year = 1950, StartYear = 1930 };
        var locations = CreateLocationService(state, catalog);
        var opportunities = new StandardLocalCareerOpportunityService(
            state,
            locations,
            catalog,
            data);

        var lwow = locations.FindTown(Lwow)!;
        var snapshot = opportunities.GetOpportunitySnapshot(lwow);

        Assert.Equal("eastern_galicia", lwow.RegionId);
        Assert.Contains("agriculture", snapshot.RegionOpportunityTags);
        Assert.Contains("oil", snapshot.RegionOpportunityTags);
    }

    [Fact]
    public void MapDataSourceReadsTownsThroughLocationServiceRatherThanTownFiles()
    {
        var root = RepositoryFiles.Root;
        var path = Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "Map",
            "Host",
            "GameMapDataSource.cs");

        var source = File.ReadAllText(path);

        Assert.True(source.Contains("_locations.GetTowns()", StringComparison.Ordinal));
        Assert.True(source.Contains("_locations.FindTown(townId)", StringComparison.Ordinal));
        Assert.False(source.Contains("dynastia-towns.json", StringComparison.OrdinalIgnoreCase));
        Assert.False(source.Contains("ReadText(", StringComparison.Ordinal));
    }

    private static HistoricalTownCatalog LoadCatalog()
    {
        var root = RepositoryFiles.Root;
        return HistoricalTownCatalog.Load(
            new JsonGameDataService(Path.Combine(root, "data")));
    }

    private static StandardLocationService CreateLocationService(
        GameState state,
        IHistoricalTownCatalog catalog,
        IGameRandom? random = null) =>
        new(
            state,
            new TestFamilyService(),
            catalog,
            random ?? new ZeroRandom(),
            new GameEventBus());

    private sealed class ZeroRandom : IGameRandom
    {
        public int NextInt(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => 0.0;
        public bool Chance(double probability) => probability > 0;
    }

    private sealed class SexAwareFamilyService : IFamilyService
    {
        private readonly Dictionary<Guid, Sex> _sexes = [];

        public void SetSex(IPerson person, Sex sex) => _sexes[person.Id] = sex;
        public void InitializePerson(IPerson person, Sex sex, int? generation = null) => SetSex(person, sex);
        public Sex GetSex(IPerson person) => _sexes.TryGetValue(person.Id, out var sex) ? sex : Sex.Male;
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
