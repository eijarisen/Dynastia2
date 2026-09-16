using Dynastia.Mechanics.Farming;

namespace Dynastia.Core.Tests;

public sealed class FarmingRulesTests
{
    [Fact]
    public void FarmlandPrices_MatchDesign()
    {
        Assert.Equal(10000m, FarmingRules.PurchasePrice);
        Assert.Equal(8000m, FarmingRules.SalePrice);
    }

    [Theory]
    [InlineData(1, 0, "")]
    [InlineData(1, 1, "0.5")]
    [InlineData(1, 2, "1.0")]
    [InlineData(2, 3, "1.0,0.5")]
    [InlineData(2, 4, "1.0,1.0")]
    [InlineData(3, 5, "1.0,1.0,0.5")]
    [InlineData(3, 6, "1.0,1.0,1.0")]
    public void StaffingFactors_MatchDesign(
        int parcels,
        int workers,
        string expectedText)
    {
        var expected = string.IsNullOrEmpty(expectedText)
            ? Array.Empty<decimal>()
            : expectedText
                .Split(',')
                .Select(value => decimal.Parse(
                    value,
                    System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();

        var actual = FarmingRules.GetStaffingFactors(parcels, workers);
        Assert.Equal(expected.Length, actual.Count);
        Assert.Equal(expected, actual.ToArray());
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
