using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Locations;
using Dynastia.Mechanics.TownLife;

namespace Dynastia.Core.Tests;

public sealed class TownLifeBatch1Tests
{
    private const string Krakow = "p_b2feaac170f8446f";

    [Fact]
    public void InstitutionInferenceUsesYearPopulationAndOpportunityContext()
    {
        var data = DataService();
        var historical = HistoricalTownCatalog.Load(data);
        var catalog = TownInstitutionCatalog.Load(data, historical);

        var noTags = new StubOpportunityService();
        var service = new StandardTownInstitutionService(catalog, noTags);

        var small = Town("synthetic-small", 4_000);
        var smallSnapshot = service.Resolve(small, 1900);
        Assert.Equal(2, smallSnapshot.GetTier("school"));
        Assert.Equal(1, smallSnapshot.GetTier("bank"));
        Assert.Equal(0, smallSnapshot.GetTier("railway_station"));

        var city = Town("synthetic-city", 60_000);
        var citySnapshot = service.Resolve(city, 1900);
        Assert.Equal(4, citySnapshot.GetTier("school"));
        Assert.Equal(4, citySnapshot.GetTier("bank"));
        Assert.Equal(3, citySnapshot.GetTier("court"));
        Assert.Equal(2, citySnapshot.GetTier("railway_station"));
    }

    [Fact]
    public void HistoricalUniversityOverridesRaiseSchoolTierWithoutChangingTownData()
    {
        var data = DataService();
        var historical = HistoricalTownCatalog.Load(data);
        var catalog = TownInstitutionCatalog.Load(data, historical);
        var service = new StandardTownInstitutionService(
            catalog,
            new StubOpportunityService());

        var krakow = historical.GetTown(Krakow, 1700);
        Assert.NotNull(krakow);

        var snapshot = service.Resolve(krakow!, 1700);

        Assert.Equal(5, snapshot.GetTier("school"));
        Assert.Equal("University", snapshot.Find("school")!.TierName);
    }

    [Fact]
    public void PortInferenceRequiresExistingPortOrShippingOpportunity()
    {
        var data = DataService();
        var historical = HistoricalTownCatalog.Load(data);
        var catalog = TownInstitutionCatalog.Load(data, historical);
        var town = Town("synthetic-port", 100_000);

        var withoutPort = new StandardTownInstitutionService(
            catalog,
            new StubOpportunityService());
        Assert.Equal(0, withoutPort.Resolve(town, 1900).GetTier("port"));

        var withShipping = new StandardTownInstitutionService(
            catalog,
            new StubOpportunityService(townTags: ["shipping"]));
        Assert.Equal(3, withShipping.Resolve(town, 1900).GetTier("port"));
    }

    [Fact]
    public void TownLifeSnapshotIsReadOnlyAndUsesCurrentLocalContext()
    {
        var data = DataService();
        var historical = HistoricalTownCatalog.Load(data);
        var catalog = TownInstitutionCatalog.Load(data, historical);
        var state = new GameState
        {
            DynastySurname = "Nowak",
            StartYear = 1900,
            Year = 1900
        };
        var person = state.CreatePerson("Jan", "Nowak", 30);
        var town = Town("snapshot-town", 25_000) with
        {
            RegionId = "test_region"
        };
        var locations = new StubLocationService(town);
        var opportunities = new StubOpportunityService(
            townTags: ["textiles"],
            regionTags: ["trade"]);
        var institutions = new StandardTownInstitutionService(catalog, opportunities);
        var prosperity = new StandardTownProsperityService(
            state,
            TownProsperityRules.Load(data));
        var facilityQuality = new StandardTownFacilityQualityService(
            institutions,
            TownFacilityQualityCatalog.Load(data));
        var service = new StandardTownLifeService(
            state,
            locations,
            opportunities,
            institutions,
            prosperity,
            facilityQuality);

        var yearBefore = state.Year;
        var peopleBefore = state.People.Count;

        var snapshot = service.GetCurrentTownLife(person);

        Assert.Equal("snapshot-town", snapshot.Town.Id);
        Assert.Equal("Test Region", snapshot.RegionName);
        Assert.Contains("textiles", snapshot.Opportunities.TownOpportunityTags);
        Assert.Contains("trade", snapshot.Opportunities.RegionOpportunityTags);
        Assert.InRange(snapshot.Prosperity.Index, 96, 104);
        Assert.Equal(yearBefore, state.Year);
        Assert.Equal(peopleBefore, state.People.Count);
        Assert.Equal(0, locations.SetCalls);
    }

    [Fact]
    public void TownAffairsIsAnActionAndUsesCompactInstitutionCenteredUi()
    {
        var root = RepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "Views", "MainWindow.axaml"));
        var townWindow = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "Views", "TownLifeWindow.axaml"));
        var townViewModel = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs"));
        var actions = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.Actions.cs"));
        var plugin = File.ReadAllText(Path.Combine(
            root, "plugins", "Dynastia.Mechanics.TownLife", "TownLifePlugin.cs"));

        Assert.DoesNotContain("OnTownLifeClick", mainWindow);
        Assert.Contains("ui.town_affairs", townViewModel);
        Assert.Contains("CreateTownAffairsPresentationAction", actions);
        Assert.Contains("Town Affairs", townViewModel);
        Assert.Contains("City Affairs", townViewModel);
        Assert.Contains("TownProsperityGraph", townWindow);
        Assert.Contains("Local Economy", townWindow);
        Assert.Contains("InstitutionCards", townWindow);
        Assert.Contains("Header=\"Institutions\"", townWindow);
        Assert.Contains("Header=\"Housing\"", townWindow);
        Assert.Contains("Header=\"Jobs\"", townWindow);
        Assert.Contains("Header=\"Education\"", townWindow);
        Assert.Contains("Header=\"Health\"", townWindow);
        Assert.DoesNotContain("Header=\"Instructions\"", townWindow);
        Assert.DoesNotContain("IActionRegistry", plugin);
        Assert.Contains("TownProsperityYearSystem", plugin);
    }

    private static TownInfo Town(string id, int population) =>
        new("Test Town", "Test County", 19.0, 52.0, population)
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
                "Test local opportunity context");
    }

    private sealed class StubLocationService : ILocationService
    {
        private readonly TownInfo _town;

        public StubLocationService(TownInfo town)
        {
            _town = town;
        }

        public int SetCalls { get; private set; }

        public LocationSnapshot GetLocation(IPerson person) =>
            new(_town, _town, null);

        public TownInfo ChoosePropertyTown(IPerson householdHead) => _town;
        public IReadOnlyList<TownInfo> GetTowns() => [_town];
        public TownInfo? FindTown(string townId) =>
            townId.Equals(_town.Id, StringComparison.OrdinalIgnoreCase) ? _town : null;

        public void SetPersonHomeTown(IPerson person, TownInfo town) => SetCalls++;
        public void SetHouseholdHomeTown(IPerson householdHead, TownInfo town) => SetCalls++;
    }
}
