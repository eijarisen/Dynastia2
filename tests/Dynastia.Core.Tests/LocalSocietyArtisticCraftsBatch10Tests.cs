using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Mechanics.Crafts;

namespace Dynastia.Core.Tests;

public sealed class LocalSocietyArtisticCraftsBatch10Tests
{
    private static readonly HashSet<string> ArtisticIds =
        new(["musician", "painter", "writer", "sculptor"], StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void CatalogAddsFourLowIncomeArtisticCraftsAndCareerLinks()
    {
        var data = CreateRepositoryData();
        var catalog = CraftCatalog.Load(data);

        Assert.Equal(29, catalog.All.Count);

        Assert.Equal(130m, catalog.Find("musician")!.BaseSalary);
        Assert.Equal(150m, catalog.Find("painter")!.BaseSalary);
        Assert.Equal(120m, catalog.Find("writer")!.BaseSalary);
        Assert.Equal(180m, catalog.Find("sculptor")!.BaseSalary);

        Assert.Equal("appeal", catalog.Find("musician")!.PrimaryStat);
        Assert.Contains("performing_arts", catalog.Find("musician")!.PrimaryCareerIds);
        Assert.Contains("newspapers_and_publishing", catalog.Find("writer")!.PrimaryCareerIds);
        Assert.Contains("construction", catalog.Find("sculptor")!.SecondaryCareerIds);

        Assert.All(
            catalog.All.Where(craft => !ArtisticIds.Contains(craft.Id)),
            craft => Assert.InRange(craft.BaseSalary, 400m, 800m));
    }

    [Fact]
    public void ArtisticTrainingUsesSchoolTierAndCityOrArtsOpportunity()
    {
        var data = CreateRepositoryData();
        var catalog = CraftCatalog.Load(data);
        var rules = ArtisticCraftTrainingRules.Load(data, catalog);

        var city = Snapshot(population: 50_000);
        var smallTown = Snapshot(population: 3_000);
        var mediaTown = Snapshot(population: 3_000, townTags: ["media"]);
        var tourismRegion = Snapshot(population: 8_000, regionTags: ["tourism"]);

        var musician = catalog.Find("musician")!;
        var painter = catalog.Find("painter")!;
        var writer = catalog.Find("writer")!;
        var sculptor = catalog.Find("sculptor")!;

        Assert.True(rules.MeetsTrainingAvailability(musician, 1900, 20, city, 3));
        Assert.True(rules.MeetsTrainingAvailability(painter, 1900, 20, city, 3));
        Assert.True(rules.MeetsTrainingAvailability(sculptor, 1900, 20, city, 3));

        Assert.False(rules.MeetsTrainingAvailability(writer, 1900, 20, city, 3));
        Assert.True(rules.MeetsTrainingAvailability(writer, 1900, 20, city, 4));

        Assert.False(rules.MeetsTrainingAvailability(musician, 1900, 20, smallTown, 3));
        Assert.True(rules.MeetsTrainingAvailability(musician, 1900, 20, mediaTown, 3));
        Assert.True(rules.MeetsTrainingAvailability(writer, 1900, 20, tourismRegion, 4));
        Assert.False(rules.MeetsTrainingAvailability(writer, 1900, 20, tourismRegion, 3));
    }

    [Fact]
    public void ArtisticTrainingRulesDoNotRestrictOrdinaryCrafts()
    {
        var data = CreateRepositoryData();
        var catalog = CraftCatalog.Load(data);
        var rules = ArtisticCraftTrainingRules.Load(data, catalog);

        var woodworking = catalog.Find("woodworking_carpentry")!;
        var smallTown = Snapshot(population: 3_000);

        Assert.True(rules.MeetsTrainingAvailability(
            woodworking,
            1900,
            woodworking.MinimumLearningAge,
            smallTown,
            0));
    }

    [Fact]
    public void KnownCraftOccupationDoesNotRecheckLocalTrainingAvailability()
    {
        var source = File.ReadAllText(RepositoryFiles.Path(
            "plugins",
            "Dynastia.Mechanics.Crafts",
            "StandardCraftService.cs"));

        var start = source.IndexOf(
            "public bool StartOccupation",
            StringComparison.Ordinal);
        var end = source.IndexOf(
            "public bool EndOccupation",
            start,
            StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        Assert.DoesNotContain(
            "MeetsTrainingAvailability",
            source[start..end]);
    }

    [Fact]
    public void SuppliedArtisticRulesKeepOrdinaryCraftSalaryBandUnchanged()
    {
        var data = CreateRepositoryData();
        var source = data.ReadText("Crafts/artistic_craft_rules.json");

        Assert.Contains("\"craftCatalogBaseSalaryMinimum\": 100", source);
        Assert.Contains("\"existingOrdinaryCraftSalaryRangeRemains\"", source);
        Assert.Contains("400", source);
        Assert.Contains("800", source);
    }

    private static LocationOpportunitySnapshot Snapshot(
        int population,
        IReadOnlyList<string>? townTags = null,
        IReadOnlyList<string>? regionTags = null)
    {
        var town = new TownInfo(
            "Test",
            "Test",
            19.0,
            52.0,
            population);

        return new LocationOpportunitySnapshot(
            town,
            "Test Region",
            regionTags ?? [],
            townTags ?? [],
            string.Empty);
    }

    private static IGameDataService CreateRepositoryData() =>
        new JsonGameDataService(RepositoryFiles.Path("data"));

}
