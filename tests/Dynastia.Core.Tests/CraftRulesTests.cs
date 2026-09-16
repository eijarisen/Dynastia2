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
    public void TeachingChance_MatchesDesign(int intellect, double expected)
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
    public void ApplicationBonus_UsesStrongestKnownCraftOnly()
    {
        var exact = new CraftInfo(
            "carpentry", "Carpentry", 1700, "🪚", "Carpenter",
            "furniture_and_carpentry", ["construction"]);
        var related = new CraftInfo(
            "masonry", "Masonry", 1700, "🧱", "Mason",
            "construction", ["furniture_and_carpentry"]);

        Assert.Equal(
            0.15,
            CraftRules.GetApplicationBonus([exact, related], "furniture_and_carpentry"),
            10);
        Assert.Equal(
            0.08,
            CraftRules.GetApplicationBonus([exact], "construction"),
            10);
        Assert.Equal(
            0.0,
            CraftRules.GetApplicationBonus([exact], "software_industry"),
            10);
    }

    [Fact]
    public void PassiveLearningChance_IsFivePercent()
    {
        Assert.Equal(0.05, CraftRules.PassiveLearningChance, 10);
    }
}
