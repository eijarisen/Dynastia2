using System.Text.Json;
using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Entities;
using Dynastia.Mechanics.Health;
using Dynastia.Mechanics.Reproduction;
using Dynastia.Mechanics.Wellbeing;

namespace Dynastia.Core.Tests;

public sealed class HealthContentReworkBatch1Tests
{
    [Fact]
    public void SharedContextResolverCombinesEraAgeSexTemperamentAndMorals()
    {
        var data = CreateRepositoryData();
        var conditions = LoadHealthDefinitions(data);
        var catalog = new ContextWeightService(data).LoadCatalog(
            "Health/health_condition_context_weights.csv",
            conditions.Select(condition => condition.Id));

        Assert.Equal(3.75, catalog.GetMultiplier(
            "tuberculosis",
            new ContextWeightContext(1750, 30, Sex.Male)), 6);
        Assert.Equal(0.225, catalog.GetMultiplier(
            "tuberculosis",
            new ContextWeightContext(2000, 30, Sex.Male)), 6);

        var measlesChild = catalog.GetMultiplier(
            "measles",
            new ContextWeightContext(1800, 8, Sex.Male));
        var measlesAdult = catalog.GetMultiplier(
            "measles",
            new ContextWeightContext(1800, 40, Sex.Male));
        Assert.True(measlesChild > measlesAdult * 5);

        var utiFemale = catalog.GetMultiplier(
            "urinary_tract_infection",
            new ContextWeightContext(1900, 30, Sex.Female));
        var utiMale = catalog.GetMultiplier(
            "urinary_tract_infection",
            new ContextWeightContext(1900, 30, Sex.Male));
        Assert.True(utiFemale > utiMale * 4);

        var melancholicDepression = catalog.GetMultiplier(
            "depression",
            new ContextWeightContext(1900, 30, Sex.Male, "Melancholic", "Neutral"));
        var sanguineDepression = catalog.GetMultiplier(
            "depression",
            new ContextWeightContext(1900, 30, Sex.Male, "Sanguine", "Neutral"));
        Assert.True(melancholicDepression > sanguineDepression);

        var goodAlcoholism = catalog.GetMultiplier(
            "alcoholism",
            new ContextWeightContext(1900, 30, Sex.Male, "Choleric", "Good"));
        var evilAlcoholism = catalog.GetMultiplier(
            "alcoholism",
            new ContextWeightContext(1900, 30, Sex.Male, "Choleric", "Evil"));
        Assert.True(evilAlcoholism > goodAlcoholism);
    }

    [Fact]
    public void SharedContextResolverRejectsInvalidCanonicalValuesWithDiagnostics()
    {
        var data = new InlineDataService(new Dictionary<string, string>
        {
            ["test.csv"] =
                "ItemId,StartYear,EndYear,Dimension,Value,WeightMultiplier\n" +
                "condition,1700,,Temperament,Stormy,1.2\n"
        });

        var error = Assert.Throws<InvalidDataException>(() =>
            new ContextWeightService(data).LoadCatalog("test.csv", ["condition"]));

        Assert.Contains("test.csv", error.Message);
        Assert.Contains("row 2", error.Message);
        Assert.Contains("Temperament", error.Message);
        Assert.Contains("Stormy", error.Message);
    }

    [Fact]
    public void HealthTargetCatalogLoadsAndPreservesIncidenceGates()
    {
        var data = CreateRepositoryData();
        var conditions = LoadHealthDefinitions(data);
        var service = new StandardHealthService(data, new FixedRandom());
        service.ConfigureHistoricalCatalog(
            HistoricalHealthCatalog.Load(data, conditions.Select(condition => condition.Id)));

        Assert.Equal(58, conditions.Count);
        Assert.Equal(0.126, HealthIncidenceRules.ScaleMildConditionChance(0.28), 6);
        Assert.Equal(0.0035, HealthIncidenceRules.ScaleSeriousConditionChance(0.01), 6);

        foreach (var id in new[] { "depression", "anxiety", "burnout", "alcoholism", "drug_dependence", "gambling_disorder" })
        {
            var definition = Assert.Single(conditions, condition => condition.Id == id);
            Assert.Equal("Mental", definition.Category);
            Assert.Equal(0d, definition.Weight);
        }
    }

    [Fact]
    public void HistoricalAvailabilityAndAgeWeightsRestrictConditionSelection()
    {
        var data = CreateRepositoryData();
        var conditions = LoadHealthDefinitions(data);
        var smallpox = conditions.Single(condition => condition.Id == "smallpox");
        var polio = conditions.Single(condition => condition.Id == "polio");
        var dementia = conditions.Single(condition => condition.Id == "dementia");

        Assert.True(StandardHealthService.IsAvailable(smallpox, age: 20, year: 1979));
        Assert.False(StandardHealthService.IsAvailable(smallpox, age: 20, year: 1980));
        Assert.True(StandardHealthService.IsAvailable(polio, age: 10, year: 2002));
        Assert.False(StandardHealthService.IsAvailable(polio, age: 10, year: 2003));
        Assert.False(StandardHealthService.IsAvailable(dementia, age: 59, year: 1900));
        Assert.True(StandardHealthService.IsAvailable(dementia, age: 70, year: 1900));

        var context = new ContextWeightService(data).LoadCatalog(
            "Health/health_condition_context_weights.csv",
            conditions.Select(condition => condition.Id));
        Assert.True(
            context.GetMultiplier("dementia", new ContextWeightContext(1900, 75))
            > context.GetMultiplier("dementia", new ContextWeightContext(1900, 60)) * 5);
    }

    [Fact]
    public void ParentsDivorcedNoLongerCausesDirectConditionDamage()
    {
        var conditions = LoadHealthDefinitions(CreateRepositoryData());
        var definition = conditions.Single(condition => condition.Id == "parents_divorced");

        Assert.Equal(0d, definition.HealthImpact);
        Assert.Equal(0d, HealthSeverityRules.ScaleAnnualImpact(definition));
    }

    [Fact]
    public void StressOutcomeCatalogIncludesDrugDependenceAndOnlyZeroWeightConditions()
    {
        var data = CreateRepositoryData();
        var conditions = LoadHealthDefinitions(data);
        var outcomes = StressOutcomeCatalog.Load(data, conditions);

        Assert.Equal(6, outcomes.Definitions.Count);
        var drugs = outcomes.Definitions.Single(outcome => outcome.ConditionId == "drug_dependence");
        Assert.Equal(18, drugs.MinimumAge);
        Assert.Equal(5, drugs.MinimumStress);
        Assert.Equal(1800, drugs.StartYear);

        var burnout = outcomes.Definitions.Single(outcome => outcome.ConditionId == "burnout");
        Assert.Equal(18, burnout.MinimumAge);
        Assert.Equal(2.5, burnout.MinimumStress, 6);
        Assert.Equal(1700, burnout.StartYear);

        var gambling = outcomes.Definitions.Single(outcome => outcome.ConditionId == "gambling_disorder");
        Assert.Equal(18, gambling.MinimumAge);
        Assert.Equal(3, gambling.MinimumStress, 6);
        Assert.Equal(0.30, gambling.BaseWeight, 6);
        Assert.Equal(1700, gambling.StartYear);

        Assert.All(outcomes.Definitions, outcome =>
            Assert.Equal(0d, conditions.Single(condition => condition.Id == outcome.ConditionId).Weight));
    }

    [Fact]
    public void MelancholicAndCholericHaveHigherStressReactionThanOtherTemperaments()
    {
        var melancholic = PersonWithTag("personality.melancholic");
        var choleric = PersonWithTag("personality.choleric");
        var sanguine = PersonWithTag("personality.sanguine");
        var phlegmatic = PersonWithTag("personality.phlegmatic");

        var mel = MentalHealthStressRules.GetReactionChance(6, melancholic, 0);
        var chol = MentalHealthStressRules.GetReactionChance(6, choleric, 0);
        var sang = MentalHealthStressRules.GetReactionChance(6, sanguine, 0);
        var phleg = MentalHealthStressRules.GetReactionChance(6, phlegmatic, 0);

        Assert.True(mel > sang);
        Assert.True(chol > sang);
        Assert.True(mel > phleg);
        Assert.True(chol > phleg);
    }

    [Fact]
    public void BirthPoolUsesMaternalAgeInsteadOfNewbornLongevity()
    {
        var data = CreateRepositoryData();
        var definitions = LoadBirthDefinitions(data);
        var context = BirthConditionContextCatalog.Load(
            data,
            definitions.Select(definition => definition.Id));

        Assert.Equal(7, definitions.Count);
        Assert.DoesNotContain(definitions, definition => definition.Id == "autism");

        var youngerRate = definitions.Sum(definition =>
            definition.Probability
            * BirthConditionRules.FrequencyScale
            * context.GetMultiplier(definition.Id, 1900, 30));
        var olderRate = definitions.Sum(definition =>
            definition.Probability
            * BirthConditionRules.FrequencyScale
            * context.GetMultiplier(definition.Id, 1900, 40));

        Assert.Equal(0.0085, youngerRate, 6);
        Assert.Equal(0.011896, olderRate, 6);
        Assert.Equal(
            BirthConditionRules.GetProbabilityScale(1),
            BirthConditionRules.GetProbabilityScale(5),
            6);
    }

    [Fact]
    public void LegacyBirthConditionIdsRemainLoadableAsHealthConditions()
    {
        var data = CreateRepositoryData();
        var health = new StandardHealthService(data, new FixedRandom());
        var person = new Person("Anna", "Test", 20);

        Assert.True(health.AddCondition(person, "autism", 1900));
        Assert.True(health.AddCondition(person, "cystic_fibrosis", 1900));
        Assert.Contains(health.GetHealth(person).Conditions, condition => condition.Id == "autism");
        Assert.Contains(health.GetHealth(person).Conditions, condition => condition.Id == "cystic_fibrosis");
    }

    [Theory]
    [InlineData(1, 0.10)]
    [InlineData(2, 0.20)]
    [InlineData(3, 0.30)]
    [InlineData(4, 0.40)]
    [InlineData(5, 0.50)]
    public void TherapySuccessRisesWithIntellect(int intellect, double expected)
    {
        Assert.Equal(expected, TherapyRules.GetSuccessChance(intellect), 6);
    }

    [Fact]
    public void AddictionAndBurnoutAreTreatableByTherapy()
    {
        Assert.True(TherapyRules.IsTreatableCondition("drug_dependence"));
        Assert.True(TherapyRules.IsTreatableCondition("burnout"));
        Assert.True(TherapyRules.IsTreatableCondition("gambling_disorder"));
    }

    [Fact]
    public void CareerStressStronglyWeightsBurnoutOutcome()
    {
        var ordinary = new StressSnapshot(5,
        [
            new StressContribution("economy.wealth_zero", 5)
        ]);
        var overworked = new StressSnapshot(5,
        [
            new StressContribution("career.low_satisfaction", 2.5),
            new StressContribution("career.overwork", 2.5)
        ]);

        Assert.Equal(1.0,
            MentalHealthStressRules.GetOutcomeWeightMultiplier("burnout", ordinary), 6);
        Assert.True(
            MentalHealthStressRules.GetOutcomeWeightMultiplier("burnout", overworked) > 3.0);
        Assert.Equal(1.0,
            MentalHealthStressRules.GetOutcomeWeightMultiplier("depression", overworked), 6);
    }

    private static Person PersonWithTag(string tag)
    {
        var person = new Person("Test", "Person", 30);
        person.Tags.Add(tag);
        return person;
    }

    private static List<HealthConditionDefinition> LoadHealthDefinitions(IGameDataService data) =>
        JsonSerializer.Deserialize<List<HealthConditionDefinition>>(
            data.ReadText("Common/health_conditions.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static List<BirthConditionDefinition> LoadBirthDefinitions(IGameDataService data) =>
        JsonSerializer.Deserialize<List<BirthConditionDefinition>>(
            data.ReadText("Common/birth_conditions.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static IGameDataService CreateRepositoryData()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var dataPath = Path.Combine(directory.FullName, "data");
            if (File.Exists(Path.Combine(dataPath, "Common", "health_conditions.json")))
                return new JsonGameDataService(dataPath);
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository data directory from test output.");
    }

    private sealed class InlineDataService : IGameDataService
    {
        private readonly IReadOnlyDictionary<string, string> _files;

        public InlineDataService(IReadOnlyDictionary<string, string> files)
        {
            _files = files;
        }

        public IReadOnlyList<string> GetStringList(string relativePath) => [];
        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(string relativePath) => [];
        public string ReadText(string relativePath) => _files[relativePath];
    }

    private sealed class FixedRandom : IGameRandom
    {
        public int NextInt(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => 0.5;
        public bool Chance(double probability) => probability >= 0.5;
    }
}
