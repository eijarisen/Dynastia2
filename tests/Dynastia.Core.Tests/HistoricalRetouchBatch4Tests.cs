using Dynastia.Contracts;
using Dynastia.Mechanics.Family;

namespace Dynastia.Core.Tests;

public sealed class HistoricalRetouchBatch4Tests
{
    private const string EraData =
        "StartYear,EndYear,MaleFile,FemaleFile\n" +
        "1700,1799,Names/m1700.csv,Names/f1700.csv\n" +
        "1800,1899,Names/m1800.csv,Names/f1800.csv\n" +
        "1900,1949,Names/m1900.csv,Names/f1900.csv\n" +
        "1950,1989,Names/m1950.csv,Names/f1950.csv\n" +
        "1990,,Names/polish_male.csv,Names/polish_female.csv\n";

    [Theory]
    [InlineData(1700, Sex.Male, "M1700")]
    [InlineData(1870, Sex.Female, "F1800")]
    [InlineData(1925, Sex.Male, "M1900")]
    [InlineData(1970, Sex.Female, "F1950")]
    [InlineData(1995, Sex.Male, "ModernM")]
    [InlineData(1682, Sex.Male, "M1700")]
    public void BirthYearSelectsExpectedHistoricalCatalogue(
        int birthYear,
        Sex sex,
        string expected)
    {
        var service = CreateService();

        var name = service.GetRandomFirstName(
            sex,
            birthYear,
            new FixedRandom(0));

        Assert.Equal(expected, name);
    }

    [Fact]
    public void DuplicateSiblingNamesAreExcludedWithinTheSameEraPool()
    {
        var service = CreateService();

        var name = service.GetRandomFirstNameExcluding(
            Sex.Male,
            1700,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "M1700"
            },
            new FixedRandom(0));

        Assert.Equal("M1700Second", name);
    }

    [Fact]
    public void EraCoverageGapsAreRejected()
    {
        const string gap =
            "StartYear,EndYear,MaleFile,FemaleFile\n" +
            "1700,1799,Names/m1700.csv,Names/f1700.csv\n" +
            "1801,,Names/polish_male.csv,Names/polish_female.csv\n";

        var data = CreateData(gap);

        Assert.Throws<InvalidDataException>(() =>
            StandardHistoricalNameService.Load(
                data,
                loadNationalityCultures: false));
    }

    [Fact]
    public void MissingReferencedCatalogueIsRejectedAtStartup()
    {
        var data = CreateData(EraData);
        data.RemoveWeighted("Names/f1800.csv");

        Assert.Throws<FileNotFoundException>(() =>
            StandardHistoricalNameService.Load(
                data,
                loadNationalityCultures: false));
    }

    private static StandardHistoricalNameService CreateService() =>
        StandardHistoricalNameService.Load(
            CreateData(EraData),
            loadNationalityCultures: false);

    private static InlineDataService CreateData(
        string eraText)
    {
        return new InlineDataService(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Names/name_eras.csv"] = eraText
            },
            new Dictionary<string, IReadOnlyList<WeightedStringEntry>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["Names/m1700.csv"] =
                    [
                        new("M1700", 100),
                        new("M1700Second", 1)
                    ],
                ["Names/f1700.csv"] = [new("F1700", 1)],
                ["Names/m1800.csv"] = [new("M1800", 1)],
                ["Names/f1800.csv"] = [new("F1800", 1)],
                ["Names/m1900.csv"] = [new("M1900", 1)],
                ["Names/f1900.csv"] = [new("F1900", 1)],
                ["Names/m1950.csv"] = [new("M1950", 1)],
                ["Names/f1950.csv"] = [new("F1950", 1)],
                ["Names/polish_male.csv"] = [new("ModernM", 1)],
                ["Names/polish_female.csv"] = [new("ModernF", 1)]
            });
    }

    private sealed class InlineDataService : IGameDataService
    {
        private readonly IReadOnlyDictionary<string, string> _text;
        private readonly Dictionary<
            string,
            IReadOnlyList<WeightedStringEntry>> _weighted;

        public InlineDataService(
            IReadOnlyDictionary<string, string> text,
            Dictionary<string, IReadOnlyList<WeightedStringEntry>> weighted)
        {
            _text = text;
            _weighted = weighted;
        }

        public void RemoveWeighted(string path) =>
            _weighted.Remove(path);

        public IReadOnlyList<string> GetStringList(string relativePath) =>
            throw new NotSupportedException();

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(
            string relativePath) =>
            _weighted.TryGetValue(relativePath, out var values)
                ? values
                : throw new FileNotFoundException(relativePath);

        public string ReadText(string relativePath) =>
            _text.TryGetValue(relativePath, out var value)
                ? value
                : throw new FileNotFoundException(relativePath);
    }

    private sealed class FixedRandom : IGameRandom
    {
        private readonly double _value;

        public FixedRandom(double value)
        {
            _value = value;
        }

        public int NextInt(int minInclusive, int maxInclusive) =>
            minInclusive;

        public double NextDouble() =>
            _value;

        public bool Chance(double probability) =>
            _value < probability;
    }
}
