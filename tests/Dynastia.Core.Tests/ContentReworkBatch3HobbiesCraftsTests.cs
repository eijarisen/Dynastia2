using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Mechanics.Crafts;
using Dynastia.Mechanics.Hobbies;

namespace Dynastia.Core.Tests;

public sealed class ContentReworkBatch3HobbiesCraftsTests
{
    [Fact]
    public void HobbyCatalogLoadsTargetSeventyThreeRowsWithBaseWeightsAndStats()
    {
        var data = CreateRepositoryData();
        var catalog = HobbyCatalog.Load(data);

        Assert.Equal(73, catalog.Hobbies.Count);
        var reading = catalog.Find("reading")!;
        Assert.Equal(1.80, reading.BaseWeight, 10);
        Assert.Equal("intellect", reading.PrimaryStat);
        Assert.Null(reading.SecondaryStat);

        var travel = catalog.Find("travel_and_sightseeing")!;
        Assert.Equal(1800, travel.StartYear);
        Assert.Equal(16, travel.MinimumAge);
    }

    [Fact]
    public void HobbyContextUsesEraAwareSexAndAgeWeights()
    {
        var data = CreateRepositoryData();
        var catalog = HobbyCatalog.Load(data);
        var context = new ContextWeightService(data)
            .LoadCatalog("Hobbies/hobby_context_weights.csv", catalog.Hobbies.Select(hobby => hobby.Id));

        var youngMale = new ContextWeightContext(1750, 20, Sex.Male, "Choleric", SettlementClass: SettlementClass.Town);
        var youngFemale = youngMale with { Sex = Sex.Female };
        var elderlyMale = youngMale with { Age = 75 };

        Assert.True(
            context.GetDimensionMultiplier("hunting", youngMale, "Sex")
            > context.GetDimensionMultiplier("hunting", youngFemale, "Sex"));
        Assert.True(
            context.GetDimensionMultiplier("hunting", youngMale, "AgeBand")
            > context.GetDimensionMultiplier("hunting", elderlyMale, "AgeBand"));
    }

    [Fact]
    public void RefinedCraftVocationDataMatchesImplementedRules()
    {
        CraftVocationDataValidation.Validate(CreateRepositoryData());
    }

    [Fact]
    public void CraftCatalogLoadsTwentyFiveBroadCraftsAndMigratesLegacyIds()
    {
        var catalog = CraftCatalog.Load(CreateRepositoryData());

        Assert.Equal(25, catalog.All.Count);
        Assert.Equal("metalworking", catalog.Find("blacksmithing")!.Id);
        Assert.Equal("woodworking_carpentry", catalog.Find("carpentry")!.Id);
        Assert.Equal("computer_hardware_repair", catalog.Find("computer_hardware")!.Id);
        Assert.Equal("welding_metal_fabrication", catalog.Find("welding")!.Id);
    }

    [Fact]
    public void CraftHardAvailabilityMatchesTargetEraAndLocalityCounts()
    {
        var catalog = CraftCatalog.Load(CreateRepositoryData());
        var none = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var shipbuilding = new HashSet<string>(["shipbuilding"], StringComparer.OrdinalIgnoreCase);

        Assert.Equal(13, CountAvailable(catalog, 1700, SettlementClass.SmallTown, none));
        Assert.Equal(13, CountAvailable(catalog, 1700, SettlementClass.Town, none));
        Assert.Equal(17, CountAvailable(catalog, 1850, SettlementClass.Town, none));
        Assert.Equal(20, CountAvailable(catalog, 1900, SettlementClass.Town, none));
        Assert.Equal(21, CountAvailable(catalog, 1900, SettlementClass.City, shipbuilding));
        Assert.Equal(22, CountAvailable(catalog, 1950, SettlementClass.MajorCity, none));
        Assert.Equal(23, CountAvailable(catalog, 2026, SettlementClass.SmallTown, none));
        Assert.Equal(23, CountAvailable(catalog, 2026, SettlementClass.MajorCity, none));
    }

    [Fact]
    public void SpecialistCraftsRequireTheirOpportunityTags()
    {
        var catalog = CraftCatalog.Load(CreateRepositoryData());
        var shipwright = catalog.Find("shipwrighting")!;
        var aircraft = catalog.Find("aircraft_maintenance")!;
        var none = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.False(shipwright.MeetsHardAvailability(1900, 30, SettlementClass.City, none));
        Assert.True(shipwright.MeetsHardAvailability(
            1900,
            30,
            SettlementClass.City,
            new HashSet<string>(["shipbuilding"], StringComparer.OrdinalIgnoreCase)));

        Assert.False(aircraft.MeetsHardAvailability(1950, 30, SettlementClass.MajorCity, none));
        Assert.True(aircraft.MeetsHardAvailability(
            1950,
            30,
            SettlementClass.MajorCity,
            new HashSet<string>(["aviation"], StringComparer.OrdinalIgnoreCase)));
    }


    [Fact]
    public void CraftMinimumLearningAgeAndOrdinarySmallTownAvailabilityAreEnforced()
    {
        var catalog = CraftCatalog.Load(CreateRepositoryData());
        var none = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var woodworking = catalog.Find("woodworking_carpentry")!;
        var electrical = catalog.Find("electrical_work")!;

        Assert.False(woodworking.MeetsHardAvailability(1700, 9, SettlementClass.SmallTown, none));
        Assert.True(woodworking.MeetsHardAvailability(1700, 10, SettlementClass.SmallTown, none));
        Assert.True(electrical.MeetsHardAvailability(2026, 30, SettlementClass.SmallTown, none));
    }

    [Fact]
    public void PreferredOpportunityTagsIncreaseWeightWithoutBecomingHardRequirements()
    {
        var catalog = CraftCatalog.Load(CreateRepositoryData());
        var metalworking = catalog.Find("metalworking")!;
        var none = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var steel = new HashSet<string>(["steel"], StringComparer.OrdinalIgnoreCase);

        Assert.True(metalworking.MeetsHardAvailability(1900, 30, SettlementClass.SmallTown, none));
        Assert.Equal(1.0, CraftRules.PreferredOpportunityMultiplier(metalworking, none), 10);
        Assert.True(CraftRules.PreferredOpportunityMultiplier(metalworking, steel) > 1.0);
    }

    [Fact]
    public void CraftContextUsesSexAndSettlementAsSoftSelectionWeights()
    {
        var data = CreateRepositoryData();
        var catalog = CraftCatalog.Load(data);
        var context = new ContextWeightService(data)
            .LoadCatalog("Crafts/craft_context_weights.csv", catalog.All.Select(craft => craft.Id));

        var maleTown = new ContextWeightContext(1750, 25, Sex.Male, "Phlegmatic", SettlementClass: SettlementClass.Town);
        var femaleTown = maleTown with { Sex = Sex.Female };
        var maleCity = maleTown with { SettlementClass = SettlementClass.MajorCity };

        Assert.NotEqual(
            context.GetDimensionMultiplier("jewellery_watchmaking", maleTown, "Sex"),
            context.GetDimensionMultiplier("jewellery_watchmaking", femaleTown, "Sex"));
        Assert.NotEqual(
            context.GetDimensionMultiplier("jewellery_watchmaking", maleTown, "SettlementClass"),
            context.GetDimensionMultiplier("jewellery_watchmaking", maleCity, "SettlementClass"));
    }

    [Fact]
    public void CraftCareerLinksAndHistoricalVariantsComeFromData()
    {
        var data = CreateRepositoryData();
        var catalog = CraftCatalog.Load(data);
        var rows = data.ReadText("Crafts/craft_career_links.csv")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(75, rows.Length); // header + 74 validated links

        var woodworking = catalog.Find("woodworking_carpentry")!;
        Assert.Contains("furniture_and_carpentry", woodworking.PrimaryCareerIds);
        Assert.Contains("construction", woodworking.PrimaryCareerIds);
        Assert.Contains("timber_and_sawmills", woodworking.SecondaryCareerIds);

        Assert.Equal("Smithing", catalog.ResolveDisplayName("metalworking", 1800));
        Assert.Equal("Metalworking", catalog.ResolveDisplayName("metalworking", 1900));
        Assert.Equal("Motor Mechanics", catalog.ResolveDisplayName("automotive_repair", 1930));
    }

    private static int CountAvailable(
        CraftCatalog catalog,
        int year,
        SettlementClass settlementClass,
        IReadOnlySet<string> opportunityTags) =>
        catalog.All.Count(craft => craft.MeetsHardAvailability(
            year,
            30,
            settlementClass,
            opportunityTags));

    private static IGameDataService CreateRepositoryData()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var dataPath = Path.Combine(directory.FullName, "data");
            if (File.Exists(Path.Combine(dataPath, "Hobbies", "hobbies.csv"))
                && File.Exists(Path.Combine(dataPath, "Crafts", "crafts.csv")))
            {
                return new JsonGameDataService(dataPath);
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository data directory from test output.");
    }
}
