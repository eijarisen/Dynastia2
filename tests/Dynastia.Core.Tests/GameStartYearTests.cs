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
    [InlineData(1700, "Polish–Lithuanian Commonwealth")]
    [InlineData(1772, "Age of Partitions")]
    [InlineData(1795, "Partitioned Lands")]
    [InlineData(1807, "Duchy of Warsaw")]
    [InlineData(1815, "Partition Era")]
    [InlineData(1863, "Late Partition Era")]
    [InlineData(1914, "First World War")]
    [InlineData(1918, "Reborn Poland")]
    [InlineData(1922, "Second Polish Republic")]
    [InlineData(1939, "Second World War")]
    [InlineData(1945, "Postwar Reconstruction")]
    [InlineData(1956, "Polish People's Republic")]
    [InlineData(1989, "Third Polish Republic")]
    [InlineData(2026, "Third Polish Republic")]
    public void HistoricalEraName_FollowsGameplayEraBoundaries(
        int year,
        string expected)
    {
        Assert.Equal(
            expected,
            HistoricalEraConfiguration.GetDisplayName(year));
    }
}
