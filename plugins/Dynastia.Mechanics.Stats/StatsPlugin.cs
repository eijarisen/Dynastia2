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

        context.AddService<IStatsService>(statsService);

        var reconciliation = context.GetService<IStateReconciliationLifecycle>()
            ?? throw new InvalidOperationException(
                "State reconciliation lifecycle is unavailable.");

        reconciliation.Register(
            "stats.components",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad,
                ReconciliationLifecycleStage.BeforeYear,
                ReconciliationLifecycleStage.AfterYear,
                ReconciliationLifecycleStage.AfterImmediateAction
            ],
            _ =>
            {
                foreach (var person in gameState.People)
                    statsService.EnsureStats(person);
            },
            order: 10);

        context.Log("Stats mechanics registered.");
    }
}
