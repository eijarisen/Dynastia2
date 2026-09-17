using Dynastia.Contracts;
using Dynastia.Core.Entities;
using Dynastia.Core.Data;
using Dynastia.Mechanics.Health;
using Dynastia.Mechanics.Justice;
using Dynastia.Mechanics.Wellbeing;

namespace Dynastia.Core.Tests;

public sealed class HistoricalRetouchBatch5Tests
{
    [Fact]
    public void HistoricalConditionNamesFollowAcquisitionYear()
    {
        var data = CreateData();
        var catalog = HistoricalHealthCatalog.Load(
            data,
            ["tuberculosis", "stroke"]);

        Assert.Equal(
            "Consumption",
            catalog.GetDisplayName("tuberculosis", "Tuberculosis", 1750));
        Assert.Equal(
            "Tuberculosis",
            catalog.GetDisplayName("tuberculosis", "Tuberculosis", 1900));
        Assert.Equal(
            "Apoplexy",
            catalog.GetDisplayName("stroke", "Stroke", 1750));
        Assert.Equal(
            "Stroke",
            catalog.GetDisplayName("stroke", "Stroke", 1946));
    }

    [Fact]
    public void HistoricalConditionPresentationPreservesIdAndHealthEffect()
    {
        var data = CreateData();
        var catalog = HistoricalHealthCatalog.Load(
            data,
            ["tuberculosis", "stroke"]);
        var service = new StandardHealthService(data, new FixedRandom());
        service.ConfigureHistoricalCatalog(catalog);
        var person = new Person("Jan", "Test", 40);

        Assert.True(service.AddCondition(person, "tuberculosis", 1750));

        var condition = Assert.Single(service.GetHealth(person).Conditions);
        Assert.Equal("tuberculosis", condition.Id);
        Assert.Equal("Consumption", condition.Name);
        Assert.Equal(-21, condition.HealthImpact);
    }

    [Fact]
    public void ExistingSerializedConditionNameIsNotRewritten()
    {
        var service = new StandardHealthService(CreateData(), new FixedRandom());
        var person = new Person("Jan", "Test", 40);
        person.Components.Set(
            new HealthComponent
            {
                Current = 80,
                Maximum = 100,
                Conditions =
                {
                    new HealthConditionState
                    {
                        Id = "tuberculosis",
                        Name = "Tuberculosis",
                        Type = "curable",
                        HealthImpact = -15,
                        RemainingYears = 3
                    }
                }
            });

        Assert.Equal(
            "Tuberculosis",
            Assert.Single(service.GetHealth(person).Conditions).Name);
    }

    [Fact]
    public void DiseaseContextWeightsChangeMixWithoutChangingIncidenceScale()
    {
        var catalog = new ContextWeightService(CreateData()).LoadCatalog(
            "Health/health_condition_context_weights.csv",
            ["tuberculosis", "stroke"]);

        Assert.Equal(2.5, catalog.GetMultiplier(
            "tuberculosis",
            new ContextWeightContext(1750, 40)), 6);
        Assert.Equal(0.15, catalog.GetMultiplier(
            "tuberculosis",
            new ContextWeightContext(2000, 40)), 6);
        Assert.Equal(1.0, catalog.GetMultiplier(
            "stroke",
            new ContextWeightContext(1750, 60)), 6);
        Assert.Equal(0.007, HealthIncidenceRules.ScaleSeriousConditionChance(0.02), 6);
        Assert.Equal(0.126, HealthIncidenceRules.ScaleMildConditionChance(0.28), 6);
    }

    [Theory]
    [InlineData(1750, 20)]
    [InlineData(1900, 25)]
    [InlineData(2000, 30)]
    public void MedicalTreatmentUsesEraHealAmount(
        int year,
        double expected)
    {
        var catalog = HealthcareEraCatalog.Load(CreateData());
        Assert.Equal(expected, catalog.GetHealAmount(year), 6);
    }

    [Fact]
    public void HistoricalCrimePresentationPreservesCrimeId()
    {
        var catalog = CrimeHistoricalCatalog.Load(
            CreateData(),
            ["vandalism", "smuggling", "fraud", "embezzlement"]);
        var crime = new CrimeDefinition
        {
            Id = "vandalism",
            Name = "vandalism",
            Category = "impulsive",
            Weight = 26,
            SentenceMin = 1,
            SentenceMax = 1,
            SuccessBase = 1,
            DetectionBase = 0.55,
            Description = "vandalizing public property"
        };

        var historical = catalog.Resolve(crime, 1750);
        var modern = catalog.Resolve(crime, 1900);

        Assert.Equal("vandalism", crime.Id);
        Assert.Equal("property destruction", historical.DisplayName);
        Assert.Equal("deliberately damaging property", historical.Description);
        Assert.Equal("vandalism", modern.DisplayName);

        var justice = new StandardJusticeService();
        var person = new Person("Jan", "Test", 40);
        justice.Imprison(
            person,
            1,
            crime.Id,
            historical.DisplayName,
            historical.Description);
        var status = justice.GetStatus(person);

        Assert.Equal("vandalism", status.CrimeId);
        Assert.Equal("property destruction", status.CrimeName);
        Assert.Equal("deliberately damaging property", status.CrimeDescription);
    }

    [Fact]
    public void ExistingSerializedConvictionTextIsNotRewritten()
    {
        var justice = new StandardJusticeService();
        var person = new Person("Jan", "Test", 40);
        person.Components.Set(
            new JusticeComponent
            {
                PrisonSentence = 3,
                CrimeId = "vandalism",
                CrimeName = "vandalism"
            });

        var status = justice.GetStatus(person);

        Assert.Equal("vandalism", status.CrimeId);
        Assert.Equal("vandalism", status.CrimeName);
        Assert.Null(status.CrimeDescription);
    }

    [Fact]
    public void CrimeEraWeightsDoNotMutateSuccessDetectionOrSentenceData()
    {
        var catalog = CrimeHistoricalCatalog.Load(
            CreateData(),
            ["vandalism", "smuggling", "fraud", "embezzlement"]);
        var crime = new CrimeDefinition
        {
            Id = "smuggling",
            Name = "smuggling",
            Category = "financial",
            Weight = 10,
            SentenceMin = 2,
            SentenceMax = 6,
            ProfitMin = 2,
            ProfitMax = 5,
            SuccessBase = 0.68,
            DetectionBase = 0.45,
            Description = "smuggling illicit goods"
        };

        Assert.Equal(1.6, catalog.GetWeightMultiplier("smuggling", 1750), 6);
        Assert.Equal(0.8, catalog.GetWeightMultiplier("smuggling", 2000), 6);
        Assert.Equal(0.68, crime.SuccessBase, 6);
        Assert.Equal(0.45, crime.DetectionBase, 6);
        Assert.Equal(2, crime.SentenceMin);
        Assert.Equal(6, crime.SentenceMax);
    }

    [Fact]
    public void ContextWeightCatalogRejectsUnknownConditionIds()
    {
        var data = CreateData(
            healthWeights:
                "ItemId,StartYear,EndYear,Dimension,Value,WeightMultiplier\n" +
                "unknown,1700,,All,,2.0\n");

        Assert.Throws<InvalidDataException>(() =>
            new ContextWeightService(data).LoadCatalog(
                "Health/health_condition_context_weights.csv",
                ["tuberculosis", "stroke"]));
    }

    private static InlineDataService CreateData(
        string? healthWeights = null)
    {
        return new InlineDataService(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Common/health_conditions.json"] =
                    """
                    [
                      {
                        "id": "tuberculosis",
                        "name": "Tuberculosis",
                        "type": "curable",
                        "category": "Serious",
                        "course": "Acute",
                        "minimumAge": 5,
                        "healthImpact": -15,
                        "weight": 6,
                        "newsworthy": true,
                        "durationMin": 3,
                        "durationMax": 8
                      },
                      {
                        "id": "stroke",
                        "name": "Stroke",
                        "type": "curable",
                        "category": "Serious",
                        "course": "Acute",
                        "minimumAge": 50,
                        "healthImpact": -6,
                        "weight": 3,
                        "newsworthy": true,
                        "durationMin": 2,
                        "durationMax": 5
                      }
                    ]
                    """,
                ["Health/health_condition_variants.csv"] =
                    "ConditionId,StartYear,EndYear,DisplayName\n" +
                    "tuberculosis,1700,1849,Consumption\n" +
                    "tuberculosis,1850,,Tuberculosis\n" +
                    "stroke,1700,1945,Apoplexy\n" +
                    "stroke,1946,,Stroke\n",
                ["Health/health_condition_context_weights.csv"] =
                    healthWeights ??
                    "ItemId,StartYear,EndYear,Dimension,Value,WeightMultiplier\n" +
                    "tuberculosis,1700,1849,All,,2.5\n" +
                    "tuberculosis,1850,1913,All,,2.0\n" +
                    "tuberculosis,1914,1945,All,,1.2\n" +
                    "tuberculosis,1946,1989,All,,0.35\n" +
                    "tuberculosis,1990,,All,,0.15\n",
                ["Health/healthcare_eras.csv"] =
                    "StartYear,EndYear,HealAmount\n" +
                    "1700,1849,20\n" +
                    "1850,1945,25\n" +
                    "1946,,30\n",
                ["Justice/crime_variants.csv"] =
                    "CrimeId,StartYear,EndYear,DisplayName,Description\n" +
                    "vandalism,1700,1793,property destruction,deliberately damaging property\n" +
                    "vandalism,1794,,vandalism,vandalizing public property\n",
                ["Justice/crime_era_weights.csv"] =
                    "CrimeId,StartYear,EndYear,WeightMultiplier\n" +
                    "smuggling,1700,1799,1.6\n" +
                    "smuggling,1800,1849,1.4\n" +
                    "smuggling,1850,1913,1.2\n" +
                    "smuggling,1914,1945,1.1\n" +
                    "smuggling,1946,1989,0.9\n" +
                    "smuggling,1990,,0.8\n" +
                    "fraud,1700,1799,0.55\n" +
                    "embezzlement,1700,1799,0.5\n"
            });
    }

    private sealed class InlineDataService : IGameDataService
    {
        private readonly IReadOnlyDictionary<string, string> _text;

        public InlineDataService(IReadOnlyDictionary<string, string> text)
        {
            _text = text;
        }

        public IReadOnlyList<string> GetStringList(string relativePath) =>
            throw new NotSupportedException();

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(
            string relativePath) =>
            throw new NotSupportedException();

        public string ReadText(string relativePath) =>
            _text.TryGetValue(relativePath, out var value)
                ? value
                : throw new FileNotFoundException(relativePath);
    }

    private sealed class FixedRandom : IGameRandom
    {
        public int NextInt(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => 0.5;
        public bool Chance(double probability) => probability >= 0.5;
    }
}
