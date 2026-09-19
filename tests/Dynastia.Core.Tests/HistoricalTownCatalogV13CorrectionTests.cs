using System.Text.Json;
using Dynastia.Core.Data;
using Dynastia.Mechanics.Locations;

namespace Dynastia.Core.Tests;

public sealed class HistoricalTownCatalogV13CorrectionTests
{
    private static readonly string[] UpperDvinaPlaces =
    [
        "p_e2d0982020b4d5b9", // Newel
        "p_9b04daf12a181223", // Siebież
        "p_b60e1736217edc0c", // Wieliż
        "p_8b075c668d1aebd1", // Druja
        "p_18c9153abc67c6f1"  // Dryssa
    ];

    [Fact]
    public void V13ManifestAndCatalogueCountsMatchCorrectedPackage()
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, "data", "Towns", "dynastia-towns.json");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var manifest = document.RootElement.GetProperty("manifest");
        var counts = manifest.GetProperty("counts");
        var catalog = Load();

        Assert.Equal("1.3.0", manifest.GetProperty("dataVersion").GetString());
        Assert.Equal(1826, counts.GetProperty("places").GetInt32());
        Assert.Equal(28, counts.GetProperty("regions").GetInt32());
        Assert.Equal(1826, catalog.PermanentPlaceCount);
        Assert.Equal(28, catalog.Regions.Count);
    }

    [Fact]
    public void V13AddsUpperDvinaAsARealRegionForTheNorthernEasternFringe()
    {
        var catalog = Load();

        Assert.Contains("upper_dvina", catalog.Regions.Keys);

        foreach (var placeId in UpperDvinaPlaces)
        {
            var town = catalog.GetTown(placeId, 1700);
            Assert.NotNull(town);
            Assert.Equal("upper_dvina", town!.RegionId);
        }
    }

    private static HistoricalTownCatalog Load()
    {
        var root = RepositoryRoot();
        return HistoricalTownCatalog.Load(
            new JsonGameDataService(Path.Combine(root, "data")));
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
