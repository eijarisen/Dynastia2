using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Mechanics.Crafts;

namespace Dynastia.Core.Tests;

public sealed class SharedMechanics4FAuthoringMetadataTests
{
    [Fact]
    public void CareerAptitudeClassificationRemainsCatalogAuthored()
    {
        var data = CreateRepositoryData();
        var rows = ReadCsv(data.ReadText("Career/careers.csv"));

        Assert.NotEmpty(rows);
        Assert.All(
            rows,
            row =>
            {
                Assert.True(
                    new[] { "strength", "intellect", "appeal" }
                        .Contains(
                            row["PrimaryStat"],
                            StringComparer.OrdinalIgnoreCase));

                var secondary = row["SecondaryStat"];
                if (!string.IsNullOrWhiteSpace(secondary)
                    && secondary != "-")
                {
                    Assert.True(
                        new[] { "strength", "intellect", "appeal" }
                            .Contains(
                                secondary,
                                StringComparer.OrdinalIgnoreCase));
                }

                Assert.False(
                    string.IsNullOrWhiteSpace(
                        row["EducationProfile"]));
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        row["CareerFamily"]));
            });
    }

    [Fact]
    public void CraftSelfEmploymentTitleComesFromCatalogRow()
    {
        var data = CreateRepositoryData();
        var source = data.ReadText("Crafts/crafts.csv");
        var overridden = source.Replace(
            ",⚒️,Metalworker",
            ",⚒️,Forge Specialist",
            StringComparison.Ordinal);

        Assert.NotEqual(source, overridden);

        var catalog = CraftCatalog.Load(
            new OverlayDataService(
                data,
                new Dictionary<string, string>
                {
                    ["Crafts/crafts.csv"] = overridden
                }));

        Assert.Equal(
            "Forge Specialist",
            catalog.Find("metalworking")?.SelfEmploymentTitle);
    }

    [Fact]
    public void CraftSelfEmploymentTitleIsValidatedAtItsCatalogField()
    {
        var data = CreateRepositoryData();
        var source = data.ReadText("Crafts/crafts.csv");
        var invalid = source.Replace(
            ",⚒️,Metalworker",
            ",⚒️,",
            StringComparison.Ordinal);

        Assert.NotEqual(source, invalid);

        var error = Assert.Throws<InvalidDataException>(() =>
            CraftCatalog.Load(
                new OverlayDataService(
                    data,
                    new Dictionary<string, string>
                    {
                        ["Crafts/crafts.csv"] = invalid
                    })));

        Assert.Contains("Crafts/crafts.csv", error.Message);
        Assert.Contains("field 'SelfEmploymentTitle'", error.Message);
        Assert.Contains("metalworking", error.Message);
        Assert.Contains("non-empty self-employment title", error.Message);
    }


    [Fact]
    public void CraftBaseSalaryComesFromCatalogAndMustRemainWithinApprovedRange()
    {
        var data = CreateRepositoryData();
        var catalog = CraftCatalog.Load(data);

        Assert.Equal(650m, catalog.Find("metalworking")!.BaseSalary);
        Assert.All(catalog.All, craft => Assert.InRange(craft.BaseSalary, 400m, 800m));

        var source = data.ReadText("Crafts/crafts.csv");
        var invalid = source.Replace(
            "metalworking,Metalworking,1700,,12,1.25,650,",
            "metalworking,Metalworking,1700,,12,1.25,900,",
            StringComparison.Ordinal);

        Assert.NotEqual(source, invalid);

        var error = Assert.Throws<InvalidDataException>(() =>
            CraftCatalog.Load(
                new OverlayDataService(
                    data,
                    new Dictionary<string, string>
                    {
                        ["Crafts/crafts.csv"] = invalid
                    })));

        Assert.Contains("field 'BaseSalary'", error.Message);
        Assert.Contains("400 through 800", error.Message);
    }

    private static IGameDataService CreateRepositoryData()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var dataPath = Path.Combine(
                directory.FullName,
                "data");

            if (File.Exists(
                    Path.Combine(
                        dataPath,
                        "Career",
                        "careers.csv")))
            {
                return new JsonGameDataService(
                    dataPath);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository data directory from test output.");
    }

    private static List<Dictionary<string, string>> ReadCsv(
        string text)
    {
        var lines = text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);

        var headers = lines[0]
            .TrimStart('\uFEFF')
            .Split(',');

        return lines
            .Skip(1)
            .Select(
                line =>
                {
                    var fields = line.Split(',');
                    return headers
                        .Select((header, index) =>
                            new KeyValuePair<string, string>(
                                header,
                                fields[index]))
                        .ToDictionary(
                            pair => pair.Key,
                            pair => pair.Value,
                            StringComparer.OrdinalIgnoreCase);
                })
            .ToList();
    }

    private sealed class OverlayDataService :
        IGameDataService
    {
        private readonly IGameDataService _inner;
        private readonly IReadOnlyDictionary<string, string> _overrides;

        public OverlayDataService(
            IGameDataService inner,
            IReadOnlyDictionary<string, string> overrides)
        {
            _inner = inner;
            _overrides = overrides;
        }

        public IReadOnlyList<string> GetStringList(
            string relativePath) =>
            _inner.GetStringList(
                relativePath);

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(
            string relativePath) =>
            _inner.GetWeightedStringList(
                relativePath);

        public string ReadText(
            string relativePath) =>
            _overrides.TryGetValue(
                relativePath,
                out var value)
                ? value
                : _inner.ReadText(
                    relativePath);
    }
}
