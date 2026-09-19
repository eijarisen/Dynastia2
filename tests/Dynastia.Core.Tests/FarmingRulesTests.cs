using Dynastia.Mechanics.Farming;

namespace Dynastia.Core.Tests;

public sealed class FarmingRulesTests
{
    [Fact]
    public void FarmlandPrices_MatchDesign()
    {
        Assert.Equal(20000m, FarmingRules.PurchasePrice);
        Assert.Equal(16000m, FarmingRules.SalePrice);
        Assert.Equal(2m, FarmingRules.WorkerBaseIncomeScale);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(1, 3, 2)]
    [InlineData(2, 3, 3)]
    [InlineData(2, 4, 4)]
    [InlineData(2, 5, 4)]
    [InlineData(3, 5, 5)]
    [InlineData(3, 6, 6)]
    public void FarmlandCapacity_AllowsTwoWorkersPerParcel(
        int parcels,
        int workers,
        int expectedActiveWorkers)
    {
        Assert.Equal(
            expectedActiveWorkers,
            FarmingRules.GetActiveWorkerCount(parcels, workers));
    }

    [Theory]
    [InlineData(1700, "1.40")]
    [InlineData(1800, "1.25")]
    [InlineData(1850, "1.10")]
    [InlineData(1900, "0.90")]
    [InlineData(1950, "0.70")]
    [InlineData(2000, "0.50")]
    [InlineData(2050, "0.50")]
    [InlineData(1750, "1.325")]
    public void EraMultiplier_InterpolatesBetweenAnchors(
        int year,
        string expectedText)
    {
        var anchors = new List<(int Year, decimal Multiplier)>
        {
            (1700, 1.40m),
            (1800, 1.25m),
            (1850, 1.10m),
            (1900, 0.90m),
            (1950, 0.70m),
            (2000, 0.50m)
        };

        var expected = decimal.Parse(
            expectedText,
            System.Globalization.CultureInfo.InvariantCulture);
        var actual = FarmingRules.InterpolateEraMultiplier(year, anchors);
        Assert.Equal(expected, actual);
    }
}
