using System.Text.Json;
using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Mechanics.Education;
using Dynastia.Mechanics.Farming;
using Dynastia.Mechanics.Health;

namespace Dynastia.Core.Tests;

public sealed class Development13AdditionsTests
{
    [Fact]
    public void GlobalWeatherFormulaHasRequiredNeutralAndExtremeAnchors()
    {
        Assert.Equal(1.00m, FarmingRules.GetRawWeatherYieldMultiplier(0, 0, 0, 0));
        Assert.Equal(2.00m, FarmingRules.GetRawWeatherYieldMultiplier(-1, 1, 1, -1));
        Assert.Equal(0.00m, FarmingRules.GetRawWeatherYieldMultiplier(1, -1, -1, 1));
        Assert.Equal(1.25m, FarmingRules.GetRawWeatherYieldMultiplier(-1, 0, 0, 0));
        Assert.Equal(0.75m, FarmingRules.GetRawWeatherYieldMultiplier(1, 0, 0, 0));
        Assert.Equal(1.25m, FarmingRules.GetRawWeatherYieldMultiplier(0, 1, 0, 0));
        Assert.Equal(1.25m, FarmingRules.GetRawWeatherYieldMultiplier(0, 0, 1, 0));
        Assert.Equal(1.25m, FarmingRules.GetRawWeatherYieldMultiplier(0, 0, 0, -1));
    }

    [Theory]
    [InlineData(1, 0.80)]
    [InlineData(2, 0.85)]
    [InlineData(3, 0.90)]
    [InlineData(4, 0.95)]
    [InlineData(5, 1.00)]
    public void PrivateTutorChanceDependsOnlyOnChildIntellect(int intellect, double expected)
    {
        Assert.Equal(expected, EducationProgressionRules.GetPrivateTutorSuccessChance(intellect), 10);
    }

    [Fact]
    public void GamblingAndPermanentInjuryConditionsUseApprovedDataContract()
    {
        var data = CreateRepositoryData();
        var definitions = JsonSerializer.Deserialize<List<HealthConditionDefinition>>(
            data.ReadText("Common/health_conditions.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var gambling = Assert.Single(definitions, item => item.Id == "gambling_disorder");
        Assert.Equal("permanent", gambling.Type);
        Assert.Equal("Mental", gambling.Category);
        Assert.Equal(18, gambling.MinimumAge);
        Assert.Equal(0, gambling.HealthImpact);
        Assert.Equal(0, gambling.Weight);
        Assert.Equal(1.0, gambling.WorkCapacityMultiplier, 10);

        AssertPermanentInjury(definitions, "chronic_pain", -1, 0.90, false);
        AssertPermanentInjury(definitions, "hearing_loss", 0, 0.95, false);
        AssertPermanentInjury(definitions, "mobility_impairment", -1, 0.70, true);
        AssertPermanentInjury(definitions, "traumatic_brain_injury", -2, 0.75, true);
        Assert.Contains(definitions, item => item.Id == "paraplegia");
    }

    [Fact]
    public void GamblingIsStressOnlyAndTutorAndWeatherAreWiredIntoExistingSurfaces()
    {
        var stress = CreateRepositoryData().ReadText("Health/health_stress_outcomes.csv");
        Assert.Contains("gambling_disorder,1700,,18,3,0.30", stress);

        var rare = CreateRepositoryData().ReadText("RareEvents/rare_events.csv");
        Assert.DoesNotContain("rare.exceptional_harvest,", rare);
        Assert.DoesNotContain("rare.crop_failure,", rare);
        Assert.Contains("rare.local_epidemic,", rare);

        var root = RepositoryFiles.Root;
        var education = File.ReadAllText(Path.Combine(root, "plugins", "Dynastia.Mechanics.Education", "EducationPlugin.cs"));
        var town = File.ReadAllText(Path.Combine(root, "src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs"));
        var farming = File.ReadAllText(Path.Combine(root, "plugins", "Dynastia.Mechanics.Farming", "StandardFarmingService.cs"));
        var health = File.ReadAllText(Path.Combine(root, "plugins", "Dynastia.Mechanics.Health", "GamblingDisorderYearSystem.cs"));

        Assert.Contains("education.private_tutor", education);
        Assert.Contains("PrivateTutorCost = 3000m", education);
        Assert.Contains("Subject is { Age: >= 6 and < 18 }", town);
        Assert.Contains("ResolveWeatherState()", farming);
        Assert.DoesNotContain("_random.NextDouble() * 2.0);\n            var adjustedMultiplier", farming);
        Assert.Contains("ChangeWealthAllowDebt(person, -amount)", health);
        Assert.Contains("After => [\"economy.household_finances\"]", health);
    }

    private static void AssertPermanentInjury(
        IReadOnlyCollection<HealthConditionDefinition> definitions,
        string id,
        double healthImpact,
        double capacity,
        bool newsworthy)
    {
        var condition = Assert.Single(definitions, item => item.Id == id);
        Assert.Equal("permanent", condition.Type);
        Assert.Equal("Injury", condition.Category);
        Assert.Equal(0, condition.Weight);
        Assert.Equal(healthImpact, condition.HealthImpact, 10);
        Assert.Equal(capacity, condition.WorkCapacityMultiplier, 10);
        Assert.Equal(newsworthy, condition.Newsworthy);
    }

    private static IGameDataService CreateRepositoryData() =>
        new JsonGameDataService(Path.Combine(RepositoryFiles.Root, "data"));

}
