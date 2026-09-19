using System.Text.Json;
using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Family;
using Dynastia.Mechanics.Locations;

namespace Dynastia.Core.Tests;

public sealed class TownsNationalitiesBatch3Tests
{
    [Fact]
    public void LoadsAllTwentyFiveNationalitiesAndAllHistoricalRegions()
    {
        var (data, names, nationalities) = CreateServices();
        var townCatalog = HistoricalTownCatalog.Load(data);

        var registryPath = Path.Combine(
            RepositoryRoot(),
            "data",
            "Nationalities",
            "nationalities.csv");

        var ids = File.ReadAllLines(registryPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Skip(1)
            .Select(line => line.Split(',')[0].TrimStart('\uFEFF').Trim())
            .ToArray();

        Assert.Equal(25, ids.Length);
        Assert.Equal(25, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var id in ids)
        {
            Assert.False(string.IsNullOrWhiteSpace(nationalities.GetDisplayName(id)));
            Assert.True(names.HasNameCulture(nationalities.GetNameCultureId(id)));
        }

        foreach (var regionId in townCatalog.Regions.Keys)
        {
            var distribution = nationalities.ResolveDistribution(regionId, 1900);
            Assert.Equal(25, distribution.Count);
            Assert.InRange(distribution.Values.Sum(), 99.999999, 100.000001);
        }
    }

    [Fact]
    public void UpperDvinaUsesEasternBelarusNationalityProfileUntilDedicatedSnapshotsExist()
    {
        var (_, _, nationalities) = CreateServices();

        var upperDvina = nationalities.ResolveDistribution("upper_dvina", 1900);
        var easternBelarus = nationalities.ResolveDistribution("eastern_belarus", 1900);

        Assert.Equal(easternBelarus.Count, upperDvina.Count);
        foreach (var (nationalityId, expectedWeight) in easternBelarus)
        {
            Assert.True(upperDvina.TryGetValue(nationalityId, out var actualWeight));
            Assert.Equal(expectedWeight, actualWeight, 8);
        }
    }

    [Fact]
    public void ExternalCultureFilesContainExpectedWeightedEntryCounts()
    {
        var root = RepositoryRoot();
        var data = new JsonGameDataService(Path.Combine(root, "data"));
        var json = File.ReadAllText(
            Path.Combine(root, "data", "Names", "name_cultures.json"));

        using var document = JsonDocument.Parse(json);
        var externalCultures = document.RootElement
            .EnumerateArray()
            .Where(element =>
                !string.Equals(
                    element.GetProperty("id").GetString(),
                    "polish",
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(24, externalCultures.Length);

        foreach (var culture in externalCultures)
        {
            var id = culture.GetProperty("id").GetString()!;
            var malePath = culture.GetProperty("maleFile").GetString()!;
            var femalePath = culture.GetProperty("femaleFile").GetString()!;
            var surnamePath = culture.GetProperty("surnameFile").GetString()!;

            var maleCount = culture.GetProperty("maleCount").GetInt32();
            var femaleCount = culture.GetProperty("femaleCount").GetInt32();
            var surnameCount = culture.GetProperty("surnameCount").GetInt32();

            Assert.True(maleCount >= 100);
            Assert.True(femaleCount >= 100);
            Assert.True(surnameCount >= 500);

            Assert.Equal(maleCount, data.GetWeightedStringList(malePath).Count);
            Assert.Equal(femaleCount, data.GetWeightedStringList(femalePath).Count);
            Assert.Equal(surnameCount, data.GetWeightedStringList(surnamePath).Count);
            Assert.False(string.IsNullOrWhiteSpace(id));
        }
    }

    [Fact]
    public void RegionalDistributionInterpolatesLinearlyAndClampsAfter2027()
    {
        var (_, _, nationalities) = CreateServices();

        const double polish1700 = 12.0606;
        const double polish1897 = 7.9362;
        const int year = 1800;

        var fraction =
            (year - 1700d)
            / (1897d - 1700d);

        var expected = polish1700
            + (polish1897 - polish1700)
            * fraction;

        var first = nationalities.ResolveDistribution(
            "eastern_belarus",
            year);

        var second = nationalities.ResolveDistribution(
            "eastern_belarus",
            year);

        Assert.Equal(first["polish"], second["polish"], 10);
        Assert.Equal(expected, first["polish"], 4);

        var afterLast = nationalities.ResolveDistribution(
            "eastern_belarus",
            2500);

        var atLast = nationalities.ResolveDistribution(
            "eastern_belarus",
            2027);

        Assert.Equal(
            atLast["polish"],
            afterLast["polish"],
            10);
    }

    [Fact]
    public void PolishNamesKeepHistoricalErasWhileExternalCulturesUseTheirOwnPools()
    {
        var (data, names, _) = CreateServices();
        var random = new ZeroRandom();

        var expectedPolish1700 =
            data.GetWeightedStringList(
                "Names/polish_male_1700.csv")[0].Value;

        var expectedGerman =
            data.GetWeightedStringList(
                "Names/Nationalities/german_male.csv")[0].Value;

        var expectedGermanSurname =
            data.GetWeightedStringList(
                "Names/Nationalities/german_surnames.csv")[0].Value;

        Assert.Equal(
            expectedPolish1700,
            names.GetRandomFirstName(
                Sex.Male,
                1700,
                "polish",
                random));

        Assert.Equal(
            expectedGerman,
            names.GetRandomFirstName(
                Sex.Male,
                1700,
                "german",
                random));

        Assert.Equal(
            expectedGermanSurname,
            names.GetRandomSurname(
                Sex.Female,
                "german",
                random));
    }

    [Fact]
    public void NonPolishSurnamesNeverReceivePolishGenderFormatting()
    {
        var (data, names, nationalities) = CreateServices();
        var state = new GameState();
        var person = state.CreatePerson("Anna", "Kowalski", 30);
        var family = new StandardFamilyService(
            state,
            data,
            names,
            nationalities);

        family.InitializePerson(person, Sex.Female);

        nationalities.SetNationality(person, "german");

        Assert.Equal(
            "Kowalski",
            nationalities.FormatSurname(
                person,
                "Kowalski",
                Sex.Female));

        Assert.Equal(
            "Kowalski",
            names.FormatSurname(
                "Kowalski",
                Sex.Female,
                "german"));

        Assert.Equal(
            "Kowalska",
            names.FormatSurname(
                "Kowalski",
                Sex.Female,
                "polish"));

        Assert.Equal(
            "Anna Kowalski",
            family.GetDisplayName(person));
    }

    [Fact]
    public void SettingNationalityChangesIdentityOnly()
    {
        var (_, _, nationalities) = CreateServices();
        var state = new GameState();
        var person = state.CreatePerson("Jan", "Nowak", 31);
        person.Tags.Add("state.alive");
        person.Tags.Add("career.employed");

        var tagsBefore = person.Tags.All
            .OrderBy(tag => tag)
            .ToArray();

        var nameBefore = person.Name;
        var surnameBefore = person.Surname;
        var ageBefore = person.Age;

        nationalities.SetNationality(person, "lithuanian");

        Assert.Equal("lithuanian", nationalities.GetNationality(person));
        Assert.Equal(nameBefore, person.Name);
        Assert.Equal(surnameBefore, person.Surname);
        Assert.Equal(ageBefore, person.Age);
        Assert.Equal(
            tagsBefore,
            person.Tags.All.OrderBy(tag => tag).ToArray());
    }

    [Fact]
    public void PersonalDetailsGeneralDisplaysNationalityWithoutFlags()
    {
        var root = RepositoryRoot();
        var xaml = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "Dynastia.App",
                "Views",
                "MainWindow.axaml"));

        Assert.True(
            xaml.Contains(
                "SelectedPerson.NationalityText",
                StringComparison.Ordinal));

        Assert.False(
            xaml.Contains(
                "NationalityFlag",
                StringComparison.OrdinalIgnoreCase));
    }

    private static (
        JsonGameDataService Data,
        StandardHistoricalNameService Names,
        StandardNationalityService Nationalities)
        CreateServices()
    {
        var data = new JsonGameDataService(
            Path.Combine(
                RepositoryRoot(),
                "data"));

        var names = StandardHistoricalNameService.Load(data);
        var nationalities = StandardNationalityService.Load(
            data,
            names);

        return (data, names, nationalities);
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

        throw new DirectoryNotFoundException(
            "Could not locate repository root.");
    }

    private sealed class ZeroRandom : IGameRandom
    {
        public int NextInt(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => 0.0;
        public bool Chance(double probability) => probability > 0.0;
    }
}
