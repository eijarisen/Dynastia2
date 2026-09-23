using System.Security.Cryptography;
using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Mechanics.Locations;

namespace Dynastia.Core.Tests;

public sealed class HistoricalTownCatalogBatch1Tests
{
    private const string Warszawa = "p_a0efc8373a9b279b";
    private const string Krakow = "p_b2feaac170f8446f";
    private const string Wroclaw = "p_133a0dacaf05301b";
    private const string Gdansk = "p_1175c98fc14211c2";
    private const string Wilno = "p_f8d111f122eae1b8";
    private const string Lwow = "p_936305d7bd070389";
    private const string Katowice = "p_a8c3eadb5c8dca2e";

    [Fact]
    public void LoadsPermanentPlacesRegionsAndMajorTownFixtures()
    {
        var catalog = Load();

        Assert.Equal(1700, catalog.MinYear);
        Assert.Equal(2027, catalog.MaxYear);
        Assert.Equal(1826, catalog.PermanentPlaceCount);
        Assert.Equal(28, catalog.Regions.Count);

        var fixtures = new Dictionary<string, string>
        {
            [Warszawa] = "Warszawa",
            [Krakow] = "Kraków",
            [Wroclaw] = "Wrocław",
            [Gdansk] = "Gdańsk",
            [Wilno] = "Wilno",
            [Lwow] = "Lwów"
        };

        foreach (var (id, expectedName) in fixtures)
        {
            var town = catalog.GetTown(id, 1937);
            Assert.NotNull(town);
            Assert.Equal(id, town.Id);
            Assert.Equal(expectedName, town.Town);
        }
    }

    [Fact]
    public void ResolvesHistoricalNamePolityAndPopulationWithoutChangingPermanentId()
    {
        var catalog = Load();

        var beforeRename = catalog.GetTown(Katowice, 1952)!;
        var duringRename = catalog.GetTown(Katowice, 1954)!;
        var afterRename = catalog.GetTown(Katowice, 1956)!;

        Assert.Equal(Katowice, beforeRename.Id);
        Assert.Equal(Katowice, duringRename.Id);
        Assert.Equal(Katowice, afterRename.Id);
        Assert.Equal("Katowice", beforeRename.Town);
        Assert.Equal("Stalinogród", duringRename.Town);
        Assert.Equal("Katowice", afterRename.Town);

        var warsaw1700 = catalog.GetTown(Warszawa, 1700)!;
        var warsaw1800 = catalog.GetTown(Warszawa, 1800)!;
        var warsaw1900 = catalog.GetTown(Warszawa, 1900)!;

        Assert.NotEqual(warsaw1700.PolityId, warsaw1800.PolityId);
        Assert.NotEqual(warsaw1700.Population, warsaw1900.Population);
        Assert.Equal(686000, warsaw1900.Population);
    }

    [Fact]
    public void PopulationUsesGeometricInterpolationBetweenSnapshots()
    {
        var catalog = Load();

        var actual = catalog.GetTown(Warszawa, 1750)!.Population;
        var fraction = 50d / 92d;
        var expected = (int)Math.Round(
            39000d * Math.Pow(120000d / 39000d, fraction),
            MidpointRounding.AwayFromZero);

        Assert.Equal(expected, actual);
        Assert.Equal(120000, catalog.GetTown(Warszawa, 1792)!.Population);
    }

    [Fact]
    public void YearsAfter2027HoldResolved2027Data()
    {
        var catalog = Load();

        var held = catalog.GetTown(Warszawa, 2050)!;
        var finalHistorical = catalog.GetTown(Warszawa, 2027)!;

        Assert.Equal(finalHistorical, held);
        Assert.Equal(
            catalog.GetAvailableTowns(2027, TownMapMode.PolishHistoryContinuity).Select(t => t.Id),
            catalog.GetAvailableTowns(2050, TownMapMode.PolishHistoryContinuity).Select(t => t.Id));
        Assert.Empty(catalog.GetEvents(2050));
    }

    [Fact]
    public void AvailabilityChangesAcrossHistoricalErasAndMapModes()
    {
        var catalog = Load();

        var continuity1772 = catalog.GetAvailableTowns(1772, TownMapMode.PolishHistoryContinuity);
        var polish1772 = catalog.GetAvailableTowns(1772, TownMapMode.PolishPolities);
        var continuity1910 = catalog.GetAvailableTowns(1910, TownMapMode.PolishHistoryContinuity);
        var continuity1950 = catalog.GetAvailableTowns(1950, TownMapMode.PolishHistoryContinuity);

        Assert.True(continuity1772.Count > polish1772.Count);
        Assert.NotEqual(continuity1772.Count, continuity1910.Count);
        Assert.NotEqual(continuity1910.Count, continuity1950.Count);
        Assert.All(continuity1950, town => Assert.True(town.IsDestinationAvailable));
    }

    [Fact]
    public void AutomaticMunicipalityResolutionAndCuratedRenameEventsAreExposed()
    {
        var catalog = Load();

        const string predecessor = "p_5e71f3272aad39db";
        const string successor = "p_7b298f47824babf8";

        Assert.Equal(predecessor, catalog.ResolveMunicipality(predecessor, 1972));
        Assert.Equal(successor, catalog.ResolveMunicipality(predecessor, 1973));

        var events = catalog.GetEvents(1953);
        Assert.Contains(events, item =>
            item.Type == "rename"
            && item.PlaceId == Katowice
            && item.PreviousName == "Katowice"
            && item.CurrentName == "Stalinogród");
    }

    [Fact]
    public void BundledTownDataMatchesPinnedInputHash()
    {
        var root = RepositoryFiles.Root;
        var path = Path.Combine(root, "data", "Towns", "dynastia-towns.json");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();

        Assert.Equal(
            "fe55ad09b74dbe894ff760eb22a471c6db1449a053f77c5f73df0e536bd53810",
            hash);
    }

    private static HistoricalTownCatalog Load()
    {
        var root = RepositoryFiles.Root;
        return HistoricalTownCatalog.Load(
            new JsonGameDataService(Path.Combine(root, "data")));
    }

}
