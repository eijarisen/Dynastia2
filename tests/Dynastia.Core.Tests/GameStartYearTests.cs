using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.Core.Tests;

public sealed class GameStartYearTests
{
    [Fact]
    public void SharedStartYear_Is1700()
    {
        Assert.Equal(
            1700,
            GameCalendarConfiguration.GameStartYear);
    }

    [Fact]
    public void FreshGameState_UsesSharedStartYear()
    {
        var gameState = new GameState();

        Assert.Equal(
            GameCalendarConfiguration.GameStartYear,
            gameState.Year);

        Assert.Equal(
            GameCalendarConfiguration.GameStartYear,
            gameState.StartYear);
    }

    [Theory]
    [InlineData(1690, 1700)]
    [InlineData(1700, 1700)]
    [InlineData(1744, 1740)]
    [InlineData(1746, 1750)]
    [InlineData(1900, 1900)]
    [InlineData(1910, 1910)]
    [InlineData(1996, 2000)]
    [InlineData(2000, 2000)]
    [InlineData(2010, 2000)]
    public void StartYearSelection_ClampsAndSnapsToDecades(
        int requested,
        int expected)
    {
        Assert.Equal(
            expected,
            GameCalendarConfiguration.NormalizeSelectableStartYear(
                requested));
    }

    [Theory]
    [InlineData(1700, "Early Modern")]
    [InlineData(1799, "Early Modern")]
    [InlineData(1800, "Early Industrial")]
    [InlineData(1849, "Early Industrial")]
    [InlineData(1850, "Industrial")]
    [InlineData(1914, "Modernizing")]
    [InlineData(1946, "Postwar")]
    [InlineData(1990, "Contemporary")]
    public void HistoricalEraName_FollowsGameplayEraBoundaries(
        int year,
        string expected)
    {
        Assert.Equal(
            expected,
            HistoricalEraConfiguration.GetDisplayName(year));
    }
}
