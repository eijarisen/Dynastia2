using Dynastia.Contracts;
using Dynastia.Mechanics.Crafts;

namespace Dynastia.Core.Tests;

public sealed class CraftRulesTests
{
    [Fact]
    public void MaximumCrafts_IsTwo()
    {
        Assert.Equal(2, CraftRules.MaximumCrafts);
    }

    [Fact]
    public void CraftEducationCost_IsTenThousand()
    {
        Assert.Equal(10000m, CraftRules.EducationCost);
    }

    [Theory]
    [InlineData(1, 0.45)]
    [InlineData(2, 0.50)]
    [InlineData(3, 0.55)]
    [InlineData(4, 0.60)]
    [InlineData(5, 0.65)]
    public void LegacyTeachingChanceScale_RemainsStable(int intellect, double expected)
    {
        Assert.Equal(expected, CraftRules.GetTeachingSuccessChance(intellect), 10);
    }

    [Theory]
    [InlineData(1700, 0.35)]
    [InlineData(1850, 0.25)]
    [InlineData(1950, 0.15)]
    [InlineData(2000, 0.10)]
    [InlineData(2050, 0.10)]
    public void GeneratedAdultChance_MatchesHistoricalAnchors(int year, double expected)
    {
        Assert.Equal(expected, CraftRules.GetGeneratedAdultBaseChance(year), 10);
    }

    [Fact]
    public void ApplicationBonus_UsesPrimaryBeforeSecondaryAndIgnoresUnrelatedCareer()
    {
        var exact = Craft(
            "woodworking_carpentry",
            "strength",
            "intellect",
            ["furniture_and_carpentry", "construction"],
            ["timber_and_sawmills"]);
        var related = Craft(
            "masonry",
            "strength",
            "intellect",
            ["construction"],
            []);

        Assert.Equal(
            0.15,
            CraftRules.GetApplicationBonus([exact, related], "furniture_and_carpentry"),
            10);
        Assert.Equal(
            0.08,
            CraftRules.GetApplicationBonus([exact], "timber_and_sawmills"),
            10);
        Assert.Equal(
            0.0,
            CraftRules.GetApplicationBonus([exact], "software_industry"),
            10);
    }

    [Fact]
    public void CraftAptitudeUsesConfiguredPrimaryAndSecondaryStats()
    {
        var heavy = Craft("metalworking", "strength", "intellect", ["blacksmithing"], []);
        var technical = Craft("radio_electronics", "intellect", null, ["radio_broadcasting"], []);

        var strong = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["strength"] = 5,
            ["intellect"] = 1
        };
        var clever = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["strength"] = 1,
            ["intellect"] = 5
        };

        Assert.True(CraftRules.GetStatSelectionMultiplier(heavy, strong)
            > CraftRules.GetStatSelectionMultiplier(heavy, clever));
        Assert.True(CraftRules.GetStatSelectionMultiplier(technical, clever)
            > CraftRules.GetStatSelectionMultiplier(technical, strong));
    }


    [Fact]
    public void MasteryLevels_UseRefinedNamesAndThresholds()
    {
        var expected = new[]
        {
            (1, "Novice", 0.0, 0),
            (2, "Apprentice", 3.0, 0),
            (3, "Adept", 8.0, 1),
            (4, "Expert", 18.0, 4),
            (5, "Master", 34.0, 10)
        };

        Assert.Equal(expected.Length, CraftRules.MasteryLevels.Count);
        foreach (var item in expected)
        {
            var rule = CraftRules.GetMasteryRule(item.Item1);
            Assert.Equal(item.Item2, rule.DisplayName);
            Assert.Equal(item.Item3, rule.RequiredMasteryProgress, 10);
            Assert.Equal(item.Item4, rule.MinimumRelevantExperienceYears);
        }
    }

    [Theory]
    [InlineData(1, 0.45)]
    [InlineData(3, 0.65)]
    [InlineData(5, 0.85)]
    public void RelevantWorkProgress_DependsOnPrimaryStat(int stat, double expected)
    {
        Assert.Equal(expected, CraftRules.GetExperienceProgressGain(stat), 10);
    }

    [Fact]
    public void MasteryExperienceGates_CannotBeBoughtAround()
    {
        Assert.Equal(4, CraftRules.GetMasteryLevel(34.0, 9));
        Assert.Equal(5, CraftRules.GetMasteryLevel(34.0, 10));
        Assert.Equal(2, CraftRules.GetMasteryLevel(8.0, 0));
        Assert.Equal(3, CraftRules.GetMasteryLevel(8.0, 1));
        Assert.Equal(3, CraftRules.GetMasteryLevel(18.0, 3));
        Assert.Equal(4, CraftRules.GetMasteryLevel(18.0, 4));
    }

    [Theory]
    [InlineData(1, 1, 0.53)]
    [InlineData(3, 3, 0.49)]
    [InlineData(5, 4, 0.55)]
    public void ExistingCraftEducationChance_MatchesRefinedFormula(
        int stat,
        int level,
        double expected)
    {
        Assert.Equal(
            expected,
            CraftRules.GetCraftImprovementChance(stat, level),
            10);
    }

    [Theory]
    [InlineData(1, 0.45)]
    [InlineData(3, 0.65)]
    [InlineData(5, 0.85)]
    public void NewCraftEducationChance_UsesPrimaryStat(int stat, double expected)
    {
        Assert.Equal(expected, CraftRules.GetNewCraftStudyChance(stat), 10);
    }

    [Theory]
    [InlineData(600, 1, 0, 606)]
    [InlineData(600, 3, 47, 1200)]
    [InlineData(600, 5, 94, 60000)]
    [InlineData(800, 5, 94, 80000)]
    public void AnnualProfessionIncome_UsesBaseMasteryAndSingleZeroToNinetyFourRoll(
        double baseSalary,
        int masteryLevel,
        int randomRoll,
        double expected)
    {
        var actual = CraftRules.CalculateAnnualIncome(
            (decimal)baseSalary,
            masteryLevel,
            randomRoll);

        Assert.Equal(expected, (double)actual, 6);
    }

    [Fact]
    public void ExpectedProfessionIncome_ScalesDirectlyFromAuthoredBaseSalary()
    {
        var lower = CraftRules.GetExpectedAnnualIncome(400m, 3);
        var higher = CraftRules.GetExpectedAnnualIncome(800m, 3);

        Assert.Equal(lower * 2m, higher);
    }

    [Fact]
    public void PassiveLearningChance_IsFivePercent()
    {
        Assert.Equal(0.05, CraftRules.PassiveLearningChance, 10);
    }

    private static CraftInfo Craft(
        string id,
        string primaryStat,
        string? secondaryStat,
        IReadOnlyList<string> primaryCareers,
        IReadOnlyList<string> secondaryCareers) =>
        new(
            id,
            id,
            1700,
            null,
            8,
            1.0,
            600m,
            primaryStat,
            secondaryStat,
            "Universal",
            SettlementClass.SmallTown,
            [],
            [],
            "🛠️",
            "Artisan",
            primaryCareers,
            secondaryCareers);
}
