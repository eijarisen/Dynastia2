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
    }
}
