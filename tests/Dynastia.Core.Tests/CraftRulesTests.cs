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
