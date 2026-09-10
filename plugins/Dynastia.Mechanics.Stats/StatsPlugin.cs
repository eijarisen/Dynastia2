using Dynastia.Contracts;

namespace Dynastia.Mechanics.Stats;

public sealed class StatsPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var gameState = context.GetService<IGameState>()
            ?? throw new InvalidOperationException(
                "Game state is unavailable.");

        var random = context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Game random service is unavailable.");

        var statsService = new StandardStatsService(random);

        foreach (var person in gameState.People)
        {
            statsService.EnsureStats(person);
        }

        context.AddService<IStatsService>(statsService);

        context.Log("Stats mechanics registered.");
    }
}
