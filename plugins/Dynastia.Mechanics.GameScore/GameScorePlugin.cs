using Dynastia.Contracts;

namespace Dynastia.Mechanics.GameScore;

public sealed class GameScorePlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var gameState = context.GetService<IGameState>() ?? throw new InvalidOperationException("Game state is unavailable.");
        var events = context.GetService<IGameEventBus>() ?? throw new InvalidOperationException("Game event bus is unavailable.");
        var family = context.GetService<IFamilyService>() ?? throw new InvalidOperationException("Family service is unavailable.");
        var households = context.GetService<IHouseholdService>() ?? throw new InvalidOperationException("Household service is unavailable.");
        var systems = context.GetService<IYearSystemRegistry>() ?? throw new InvalidOperationException("Year-system registry is unavailable.");
        var reconciliation = context.GetService<IStateReconciliationLifecycle>() ?? throw new InvalidOperationException("Reconciliation lifecycle is unavailable.");

        var score = new StandardGameScoreService(
            gameState,
            events,
            family,
            households,
            context.GetService<IPersonLookup>(),
            context.GetService<ICareerService>(),
            context.GetService<ICraftService>(),
            context.GetService<ICriminalOccupationService>(),
            context.GetService<ICivicOfficeService>());

        context.AddService<IGameScoreService>(score);
        context.AddService<IGameScorePreviewService>(score);
        systems.Register(new GameScoreClaimReconcileYearSystem(score));

        reconciliation.Register(
            "game_score.reconcile",
            [ReconciliationLifecycleStage.AfterNewGame, ReconciliationLifecycleStage.AfterLoad],
            stage =>
            {
                if (stage == ReconciliationLifecycleStage.AfterNewGame) score.ReconcileAfterNewGame();
                else score.ReconcileAfterLoad();
            },
            order: 200);

        context.Log("Game score mechanics registered.");
    }
}
