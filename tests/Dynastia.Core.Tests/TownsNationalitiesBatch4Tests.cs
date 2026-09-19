using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Family;
using Dynastia.Mechanics.Relationships;
using Dynastia.Mechanics.Reproduction;

namespace Dynastia.Core.Tests;

public sealed class TownsNationalitiesBatch4Tests
{
    [Fact]
    public void GermanRegionOutsiderCanGenerateGermanIdentityAndNames()
    {
        var (data, names, nationalities) = CreateServices();
        var generator = new StandardOutsiderIdentityService(nationalities, names);
        var town = Town("lower_silesia");
        var random = new ConstantRandom(0.10);

        var identity = generator.Generate(town, Sex.Male, 1870, 1897, random);

        Assert.Equal("german", identity.NationalityId);
        Assert.Equal("german", identity.NameCultureId);
        Assert.Contains(identity.FirstName, data.GetWeightedStringList("Names/Nationalities/german_male.csv").Select(x => x.Value));
        Assert.Contains(identity.Surname, data.GetWeightedStringList("Names/Nationalities/german_surnames.csv").Select(x => x.Value));
    }

    [Fact]
    public void VilniusOutsiderUsesHistoricalMultiNationalPool()
    {
        var (_, names, nationalities) = CreateServices();
        var generator = new StandardOutsiderIdentityService(nationalities, names);
        var town = Town("vilnius");

        var polish = generator.Generate(town, Sex.Female, 1910, 1931, new ConstantRandom(0.10));
        var belarusian = generator.Generate(town, Sex.Female, 1910, 1931, new ConstantRandom(0.70));

        Assert.Equal("polish", polish.NationalityId);
        Assert.Equal("belarusian", belarusian.NationalityId);
        Assert.NotEqual(polish.NameCultureId, belarusian.NameCultureId);
    }

    [Fact]
    public void GeneratedFamilyBackgroundUsesCandidateCulture()
    {
        var (data, names, nationalities) = CreateServices();
        var state = new GameState();
        var family = new StandardFamilyService(state, data, names, nationalities);
        var person = state.CreatePerson("Karl", "Schmidt", 25);
        person.BirthDate = new GameDate(1900, 1, 1);
        family.InitializePerson(person, Sex.Male);
        nationalities.SetNationality(person, "german");

        GeneratedFamilyBackgroundGenerator.Assign(
            person,
            "Schmidt",
            "german",
            family,
            names,
            new ZeroRandom());

        var background = family.GetGeneratedFamilyBackground(person)!;
        var expectedFather = data.GetWeightedStringList("Names/Nationalities/german_male.csv")[0].Value;
        var expectedMother = data.GetWeightedStringList("Names/Nationalities/german_female.csv")[0].Value;

        Assert.StartsWith(expectedFather + " ", background.FatherName);
        Assert.StartsWith(expectedMother + " ", background.MotherName);
    }

    [Fact]
    public void ChildAlwaysInheritsKnownFatherNationality()
    {
        var (_, _, nationalities) = CreateServices();
        var town = Town("masovia");

        Assert.Equal(
            "polish",
            ChildNationalityRules.Resolve("polish", "ukrainian", town, 2020, nationalities, new ConstantRandom(0.10)));

        Assert.Equal(
            "polish",
            ChildNationalityRules.Resolve("polish", "ukrainian", town, 2020, nationalities, new ConstantRandom(0.90)));

        Assert.Equal(
            "german",
            ChildNationalityRules.Resolve("german", "polish", town, 2020, nationalities, new ConstantRandom(0.10)));
    }

    [Fact]
    public void PolishPartnerPreferenceHalvesForeignCandidateShareWithoutChangingRegionalData()
    {
        var source = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["polish"] = 60.0,
            ["german"] = 30.0,
            ["jewish"] = 10.0
        };

        var adjusted =
            PartnerNationalityPreferenceRules.AdjustDistribution(
                source,
                "polish");

        Assert.Equal(80.0, adjusted["polish"], 6);
        Assert.Equal(15.0, adjusted["german"], 6);
        Assert.Equal(5.0, adjusted["jewish"], 6);
        Assert.Equal(60.0, source["polish"], 6);
        Assert.Equal(30.0, source["german"], 6);
        Assert.Equal(10.0, source["jewish"], 6);

        var unchanged =
            PartnerNationalityPreferenceRules.AdjustDistribution(
                source,
                "ukrainian");

        Assert.Same(source, unchanged);
    }

    [Fact]
    public void MarriageDoesNotChangePersistedNationality()
    {
        var (data, names, nationalities) = CreateServices();
        var state = new GameState();
        var family = new StandardFamilyService(state, data, names, nationalities);
        var first = state.CreatePerson("Jan", "Nowak", 30);
        var second = state.CreatePerson("Olena", "Koval", 28);
        family.InitializePerson(first, Sex.Male);
        family.InitializePerson(second, Sex.Female);
        nationalities.SetNationality(first, "polish");
        nationalities.SetNationality(second, "ukrainian");

        family.SetSpouses(first, second, 2020);
        second.Surname = first.Surname;

        Assert.Equal("polish", nationalities.GetNationality(first));
        Assert.Equal("ukrainian", nationalities.GetNationality(second));
    }

    [Fact]
    public void CandidateAndLoanPresentationExposeOriginAndNationality()
    {
        var root = RepositoryRoot();
        var partnerXaml = File.ReadAllText(Path.Combine(root, "src", "Dynastia.App", "Views", "PotentialPartnersWindow.axaml"));
        var loanXaml = File.ReadAllText(Path.Combine(root, "src", "Dynastia.App", "Views", "LoanSelectionWindow.axaml"));
        var partnerSource = File.ReadAllText(Path.Combine(root, "plugins", "Dynastia.Mechanics.Relationships", "StandardPartnerSearchService.cs"));
        var loanSource = File.ReadAllText(Path.Combine(root, "plugins", "Dynastia.Mechanics.Loans", "StandardLoanService.cs"));

        Assert.Contains("OriginNationalityText", partnerXaml);
        Assert.Contains("OriginNationalityText", loanXaml);
        Assert.Contains("_outsiderIdentities.Generate", partnerSource);
        Assert.Contains("_outsiderIdentities.Generate", loanSource);
        Assert.True(
            loanSource.IndexOf("var originTown", StringComparison.Ordinal)
            < loanSource.IndexOf("_outsiderIdentities.Generate", StringComparison.Ordinal));
        Assert.False(partnerSource.Contains("Names/polish_surnames.csv", StringComparison.OrdinalIgnoreCase));
        Assert.False(loanSource.Contains("Names/polish_surnames.csv", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NationalityIsNotAStandaloneMechanicalModifier()
    {
        var root = RepositoryRoot();
        var partnerRules = File.ReadAllText(Path.Combine(root, "plugins", "Dynastia.Mechanics.Relationships", "PartnerSearchRules.cs"));

        Assert.False(partnerRules.Contains("nationality", StringComparison.OrdinalIgnoreCase));

        foreach (var mechanic in new[] { "Career", "Health", "Justice" })
        {
            var directory = Path.Combine(root, "plugins", $"Dynastia.Mechanics.{mechanic}");
            var combined = string.Join(
                "\n",
                Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                    .Select(File.ReadAllText));

            Assert.False(combined.Contains("INationalityService", StringComparison.Ordinal));
            Assert.False(combined.Contains("NationalityId", StringComparison.Ordinal));
        }
    }

    private static TownInfo Town(string regionId) =>
        new("Test", "Test", 21.0, 52.0, 100_000)
        {
            Id = "test-" + regionId,
            RegionId = regionId,
            IsDestinationAvailable = true
        };

    private static (JsonGameDataService Data, StandardHistoricalNameService Names, StandardNationalityService Nationalities) CreateServices()
    {
        var data = new JsonGameDataService(Path.Combine(RepositoryRoot(), "data"));
        var names = StandardHistoricalNameService.Load(data);
        var nationalities = StandardNationalityService.Load(data, names);
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
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class ConstantRandom(double value) : IGameRandom
    {
        public int NextInt(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => value;
        public bool Chance(double probability) => value < probability;
    }

    private sealed class ZeroRandom : IGameRandom
    {
        public int NextInt(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => 0.0;
        public bool Chance(double probability) => probability > 0.0;
    }
}
