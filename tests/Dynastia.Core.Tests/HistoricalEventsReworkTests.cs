using System.Reflection;
using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Plugins;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Family;
using Dynastia.Mechanics.Historical;
using Dynastia.Mechanics.Locations;

namespace Dynastia.Core.Tests;

public sealed class HistoricalEventsReworkTests
{
    private const string WarsawId = "p_a0efc8373a9b279b";
    private const string PoznanId = "p_faf2572ac2e7d16a";
    private const string LwowId = "p_936305d7bd070389";
    private const string WroclawId = "p_133a0dacaf05301b";
    private const string KrakowId = "p_b2feaac170f8446f";
    private const string BialystokId = "p_f3c0a46e248e8166";

    [Fact]
    public void PackageLoadsExpectedFrameworkAndFull1700To2026Content()
    {
        var (catalog, _, _) = LoadCatalog();

        Assert.Equal(52, catalog.Events.Count);
        Assert.Equal(34, catalog.Profiles.Count);
        Assert.Equal(50, catalog.Scopes.Count);
        Assert.Equal(9, catalog.Filters.Count);
        Assert.Equal(10, catalog.Routes.Count);
        Assert.Equal(10, catalog.CandidateModifiers.Count);
        Assert.Equal(13, catalog.Periods.Count);

        Assert.Equal(27, catalog.Events.Count(item => item.StartYear <= 1938));
        Assert.Equal(25, catalog.Events.Count(item => item.StartYear >= 1939));
        Assert.Equal(1700, catalog.Events.Min(item => item.StartYear));
        Assert.Equal(2026, catalog.Events.Max(item => item.EndYear));
    }

    [Fact]
    public void DynamicAndTownSpecificScopesResolveAgainstPermanentTownHistory()
    {
        var (catalog, towns, _) = LoadCatalog();
        var resolver = new HistoricalEventScopeResolver(catalog, towns);

        Assert.True(resolver.GetMultiplier("first_partition_places", LwowId, 1772) > 0);
        Assert.Equal(0, resolver.GetMultiplier("first_partition_places", WarsawId, 1772));

        Assert.True(resolver.GetMultiplier("warsaw_only", WarsawId, 1944) > 0);
        Assert.Equal(0, resolver.GetMultiplier("warsaw_only", PoznanId, 1944));
    }

    [Fact]
    public void SensitiveTargetFiltersStayEventSpecific()
    {
        var (catalog, _, _) = LoadCatalog();

        var jewish = catalog.Filters["jewish_members"];
        var ukrainian = catalog.Filters["ukrainian_household"];
        var german = catalog.Filters["german_household"];

        Assert.Equal("jewish", Assert.Single(jewish.NationalityIds!));
        Assert.Equal("ukrainian", Assert.Single(ukrainian.NationalityIds!));
        Assert.Equal("german", Assert.Single(german.NationalityIds!));

        Assert.Equal(
            "jewish_members",
            catalog.Events.Single(item => item.Id == "holocaust_mass_murder").TargetFilterId);
        Assert.Equal(
            "ukrainian_household",
            catalog.Events.Single(item => item.Id == "operation_vistula").TargetFilterId);
        Assert.Equal(
            "german_household",
            catalog.Events.Single(item => item.Id == "german_postwar_expulsion").TargetFilterId);
    }

    [Fact]
    public void MigrationRoutesPreserveRequiredDestinationGuards()
    {
        var (catalog, _, _) = LoadCatalog();

        var warsaw = catalog.Routes["warsaw_evacuation"];
        Assert.Contains(WarsawId, warsaw.ExcludePlaceIds!);
        var kresy = catalog.Routes["kresy_to_west"];
        Assert.Contains("lower_silesia", kresy.TargetRegionIds!);
        Assert.Contains("west_pomerania", kresy.TargetRegionIds!);

        var vistula = catalog.Routes["operation_vistula_west"];
        Assert.Contains("warmia_masuria", vistula.TargetRegionIds!);
        Assert.Contains("lubusz", vistula.TargetRegionIds!);

        var exchange = catalog.Routes["exchange_1951_relocation"];
        Assert.NotEmpty(exchange.TargetRegionIds ?? []);
        Assert.True(exchange.LoseOriginHouse.ValueKind is System.Text.Json.JsonValueKind.True);
        Assert.True(exchange.LoseOriginFarmland.ValueKind is System.Text.Json.JsonValueKind.True);

        Assert.Equal("Soviet Union (exile)", catalog.Routes["soviet_exile"].DestinationLabel);
        Assert.Equal("Germany", catalog.Routes["german_expulsion"].DestinationLabel);
    }


    [Fact]
    public void EarlyContentUsesExpectedRegionalScopesAndEconomicMultipliers()
    {
        var (catalog, towns, _) = LoadCatalog();
        var resolver = new HistoricalEventScopeResolver(catalog, towns);

        Assert.True(resolver.GetMultiplier("november_uprising_regions", WarsawId, 1830) > 0);
        Assert.Equal(0, resolver.GetMultiplier("november_uprising_regions", PoznanId, 1830));
        Assert.True(resolver.GetMultiplier("great_retreat_regions", BialystokId, 1915) > 0);
        Assert.Equal(0, resolver.GetMultiplier("great_retreat_regions", PoznanId, 1915));
        Assert.True(resolver.GetMultiplier("southern_flood_1934_regions", KrakowId, 1934) > 0);
        Assert.Equal(0, resolver.GetMultiplier("southern_flood_1934_regions", WarsawId, 1934));

        var depression = catalog.Profiles["economic_depression"];
        Assert.Equal(1.6, depression.HouseholdExposureMultipliers!["industrial_or_transport_worker"], 6);
        Assert.Equal(1.25, depression.HouseholdExposureMultipliers["unemployed"], 6);

        var influenza = catalog.Events.Single(item => item.Id == "spanish_influenza");
        Assert.Equal("epidemic", influenza.ExclusiveGroup);
        Assert.Equal(1918, influenza.StartYear);
        Assert.Equal(1920, influenza.EndYear);
    }

    [Fact]
    public void PostwarAndModernContentUsesDynamicTerritoryFloodAndAgeRules()
    {
        var (catalog, towns, _) = LoadCatalog();
        var resolver = new HistoricalEventScopeResolver(catalog, towns);

        var cededPlaces = towns.PermanentPlaceIds
            .Where(id => resolver.GetMultiplier("ceded_1951", id, 1951) > 0)
            .ToList();
        Assert.NotEmpty(cededPlaces);

        Assert.True(resolver.GetMultiplier("flood_1997_regions", WroclawId, 1997) > 0);
        Assert.Equal(0, resolver.GetMultiplier("flood_1997_regions", WarsawId, 1997));

        var covid = catalog.Profiles["covid_epidemic"];
        Assert.Equal(6.0, covid.PersonRiskMultipliers!["age_70_plus"], 6);
        Assert.Equal(2.5, covid.PersonRiskMultipliers["age_55_69"], 6);
        Assert.Equal(1.3, covid.PersonRiskMultipliers["age_35_54"], 6);

        var march = catalog.Events.Single(item => item.Id == "march_1968");
        Assert.Equal("jewish_household", march.TargetFilterId);
    }

    [Fact]
    public void PeriodDataMatchesDisplayedHistoricalEraLabels()
    {
        var (catalog, _, _) = LoadCatalog();

        foreach (var period in catalog.Periods)
        {
            Assert.Equal(period.DisplayName, HistoricalEraConfiguration.GetDisplayName(period.StartYear));
            Assert.Equal(period.DisplayName, HistoricalEraConfiguration.GetDisplayName(Math.Min(period.EndYear, 2026)));
        }
    }

    [Fact]
    public void UkrainianRefugeeModifierRaisesShareThenRenormalizes()
    {
        var root = RepositoryRoot();
        var data = new JsonGameDataService(Path.Combine(root, "data"));
        var names = StandardHistoricalNameService.Load(data);
        var baseline = StandardNationalityService.Load(data, names);
        var baselineDistribution = baseline.ResolveDistribution("masovia", 2022);

        var modified = StandardNationalityService.Load(data, names);
        var towns = HistoricalTownCatalog.Load(data);
        var context = new GamePluginContext();
        context.AddService<IGameDataService>(data);
        context.AddService<IHistoricalTownCatalog>(towns);
        context.AddService<INationalityService>(modified);
        context.AddService<IYearSystemRegistry>(new YearSystemRegistry());

        new HistoricalPlugin().Initialize(context);

        var modifiedDistribution = modified.ResolveDistribution("masovia", 2022);

        Assert.True(
            modifiedDistribution["ukrainian"] > baselineDistribution["ukrainian"]);
        Assert.InRange(modifiedDistribution.Values.Sum(), 99.999999, 100.000001);
        Assert.NotNull(context.GetService<IHistoricalEventService>());
    }

    [Fact]
    public void HistoricalSystemRegistersInPreYearWithoutUsingRareEventGate()
    {
        var root = RepositoryRoot();
        var data = new JsonGameDataService(Path.Combine(root, "data"));
        var names = StandardHistoricalNameService.Load(data);
        var nationalities = StandardNationalityService.Load(data, names);
        var towns = HistoricalTownCatalog.Load(data);
        var registry = new YearSystemRegistry();
        var context = new GamePluginContext();
        context.AddService<IGameDataService>(data);
        context.AddService<IHistoricalTownCatalog>(towns);
        context.AddService<INationalityService>(nationalities);
        context.AddService<IYearSystemRegistry>(registry);

        new HistoricalPlugin().Initialize(context);

        var system = Assert.Single(registry.Systems, item => item.Id == "historical.events");
        Assert.Equal(YearPhase.PreYear, system.Phase);
        Assert.False(system.Id.Contains("rare", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HistoricalBalanceDampeningIsExplicitAndBounded()
    {
        Assert.Equal(0.55, HistoricalEventYearSystem.RecurringExposureScale, 6);
        Assert.Equal(0.85, HistoricalEventYearSystem.OneShotExposureScale, 6);
        Assert.Equal(0.60, HistoricalEventYearSystem.WealthSeverityScale, 6);
        Assert.Equal(0.70, HistoricalEventYearSystem.HealthSeverityScale, 6);
        Assert.Equal(0.35, HistoricalEventYearSystem.DeathSeverityScale, 6);
        Assert.Equal(0.28, HistoricalEventYearSystem.StressSeverityScale, 6);
    }

    [Fact]
    public void HistoricalStateComponentsHaveStableSaveIds()
    {
        var handled = typeof(HistoricalEventStateComponent)
            .GetCustomAttribute<PersistedComponentIdAttribute>();
        Assert.NotNull(handled);
        Assert.Equal("historical.events", handled!.Id);

        var externalResidence = typeof(ExternalResidenceComponent)
            .GetCustomAttribute<PersistedComponentIdAttribute>();
        Assert.NotNull(externalResidence);
        Assert.Equal("historical.external_residence", externalResidence!.Id);
    }

    [Fact]
    public void HistoricalMilestonesCarryPlayerFacingDescriptions()
    {
        var (catalog, _, _) = LoadCatalog();

        Assert.All(
            catalog.Events.Where(item => item.ChronicleAtStart),
            item =>
            {
                Assert.False(string.IsNullOrWhiteSpace(item.GlobalNewsDescription));
                var description = item.GlobalNewsDescription.ToLowerInvariant();
                Assert.DoesNotContain("use historical_", description);
                Assert.DoesNotContain("game derives", description);
            });

        var occupation = catalog.Events.Single(item => item.Id == "occupation_hardship");
        Assert.Contains("occupation", occupation.GlobalNewsDescription.ToLowerInvariant());
    }

    [Fact]
    public void PartitionMilestonesDescribeOnlyTheCurrentPartition()
    {
        var (catalog, _, _) = LoadCatalog();

        var first = catalog.Events.Single(item => item.Id == "first_partition");
        Assert.Contains("1772", first.GlobalNewsDescription);
        Assert.DoesNotContain("1793", first.GlobalNewsDescription);
        Assert.DoesNotContain("1795", first.GlobalNewsDescription);

        var second = catalog.Events.Single(item => item.Id == "second_partition");
        Assert.Contains("1793", second.GlobalNewsDescription);
        Assert.DoesNotContain("1795", second.GlobalNewsDescription);

        var third = catalog.Events.Single(item => item.Id == "third_partition");
        Assert.Contains("1795", third.GlobalNewsDescription);
    }

    [Fact]
    public void HistoricalNewsPresentationUsesGlobalNewsAndExactEventYears()
    {
        var root = RepositoryRoot();
        var flow = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "ViewModels",
            "MainWindowViewModel.GameFlow.cs"));
        var main = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "ViewModels",
            "MainWindowViewModel.cs"));
        var historical = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Historical",
            "HistoricalEventYearSystem.cs"));

        Assert.Contains("\"Global News\"", flow);
        Assert.Contains("GlobalNewsChronicleGroupId", flow);
        Assert.Contains("int.MaxValue", flow);
        Assert.Contains("AlbumDisplayYear =>", main);
        Assert.Contains("AlbumYear;", main);
        Assert.DoesNotContain("AlbumYear - 1", main);
        Assert.Contains("$\"Year {_yearSummaryEventYear}\"", main);
        Assert.DoesNotContain("_yearSummaryEventYear - 1", main);
        Assert.DoesNotContain("_gameState.StartYear + 1", main);
        Assert.DoesNotContain("_gameState.StartYear + 1", flow);
        Assert.Contains("[\"globalNews\"] = \"true\"", historical);
        Assert.Contains("Financial losses totaled", historical);
        Assert.DoesNotContain("string.Join(\"; \", financialParts)", historical);
        Assert.Contains("was relocated from {from} to {to}", historical);
    }

    private static (HistoricalEventCatalog Catalog, HistoricalTownCatalog Towns, INationalityService Nationalities) LoadCatalog()
    {
        var root = RepositoryRoot();
        var data = new JsonGameDataService(Path.Combine(root, "data"));
        var names = StandardHistoricalNameService.Load(data);
        var nationalities = StandardNationalityService.Load(data, names);
        var towns = HistoricalTownCatalog.Load(data);
        return (HistoricalEventCatalog.Load(data, towns, nationalities), towns, nationalities);
    }

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
}
