using Dynastia.Contracts;

namespace Dynastia.Core.Tests;

public sealed class TownEconomicIndexTests
{
    [Theory]
    [InlineData(4999, SettlementClass.SmallTown, 0.75, 0.85)]
    [InlineData(5000, SettlementClass.Town, 0.90, 0.95)]
    [InlineData(19999, SettlementClass.Town, 0.90, 0.95)]
    [InlineData(20000, SettlementClass.City, 1.10, 1.05)]
    [InlineData(99999, SettlementClass.City, 1.10, 1.05)]
    [InlineData(100000, SettlementClass.MajorCity, 1.35, 1.20)]
    public void SettlementClassDefinesStaticEconomicIndexes(
        int population,
        SettlementClass expectedClass,
        double expectedHousing,
        double expectedLiving)
    {
        var town = new TownInfo(
            "Test",
            "Test County",
            0,
            0,
            population);

        Assert.Equal(expectedClass, town.SettlementClass);
        Assert.Equal((decimal)expectedHousing, town.HousingIndex);
        Assert.Equal((decimal)expectedLiving, town.LivingCostIndex);
    }

    [Theory]
    [InlineData(1000, "Small Town")]
    [InlineData(10000, "Town")]
    [InlineData(50000, "City")]
    [InlineData(250000, "Major City")]
    public void SettlementClassDisplayNameUsesReadableSpacing(
        int population,
        string expected)
    {
        var town = new TownInfo(
            "Test",
            "Test County",
            0,
            0,
            population);

        Assert.Equal(expected, town.SettlementClassDisplayName);
    }

    [Fact]
    public void MoralsReflectionRunsAfterPostYearAndBeforeThoughts()
    {
        Assert.True(YearPhase.MoralsReflection > YearPhase.PostYear);
        Assert.True(YearPhase.MoralsReflection < YearPhase.Thoughts);
    }
}
