using Dynastia.Contracts;

namespace Dynastia.Core.Simulation;

public sealed class YearProcessor
{
    private readonly IGameState _gameState;
    private readonly IYearSystemRegistry _registry;

    public YearProcessor(
        IGameState gameState,
        IYearSystemRegistry registry)
    {
        _gameState = gameState;
        _registry = registry;
    }

    public void AdvanceYear()
    {
        _gameState.Year++;

        var systems = _registry.Systems
            .OrderBy(system => system.Phase)
            .ThenBy(system => system.Id)
            .ToList();

        foreach (var system in systems)
        {
            Console.WriteLine($"[{_gameState.Year}] Running {system.Id}");
            system.Execute(_gameState);
        }
    }
}
