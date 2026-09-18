using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

public sealed class FamilyRelationsPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var gameState = context.GetService<IGameState>() ?? throw new InvalidOperationException("Game state is unavailable.");
        var family = context.GetService<IFamilyService>() ?? throw new InvalidOperationException("Family service is unavailable.");
        var economy = context.GetService<IEconomyService>() ?? throw new InvalidOperationException("Economy service is unavailable.");
        var households = context.GetService<IHouseholdService>() ?? throw new InvalidOperationException("Household service is unavailable.");
        var marriage = context.GetService<IMarriageSatisfactionService>() ?? throw new InvalidOperationException("Marriage satisfaction service is unavailable.");
        var career = context.GetService<ICareerService>() ?? throw new InvalidOperationException("Career service is unavailable.");
        var locations = context.GetService<ILocationService>() ?? throw new InvalidOperationException("Location service is unavailable.");
        var personality = context.GetService<IPersonalityService>();
        var random = context.GetService<IGameRandom>() ?? throw new InvalidOperationException("Random service is unavailable.");
        var events = context.GetService<IGameEventBus>() ?? throw new InvalidOperationException("Game event bus is unavailable.");
        var actions = context.GetService<IActionRegistry>() ?? throw new InvalidOperationException("Action registry is unavailable.");
        var systems = context.GetService<IYearSystemRegistry>() ?? throw new InvalidOperationException("Year system registry is unavailable.");

        var relations = new StandardFamilyRelationService(
            gameState, family, economy, households, personality, marriage, random);
        context.AddService<IFamilyRelationService>(relations);

        var reconciliation = context.GetService<IStateReconciliationLifecycle>()
            ?? throw new InvalidOperationException(
                "State reconciliation lifecycle is unavailable.");

        reconciliation.Register(
            "family_relations.network",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad,
                ReconciliationLifecycleStage.BeforeYear,
                ReconciliationLifecycleStage.AfterYear,
                ReconciliationLifecycleStage.AfterImmediateAction,
                ReconciliationLifecycleStage.AfterQueuedAction,
                ReconciliationLifecycleStage.AfterPersonCreated
            ],
            _ => relations.ReconcileAll(),
            order: 80);

        _ = new FamilyRelationEventBridge(gameState, family, economy, households, relations, events);
        systems.Register(new FamilyRelationYearSystem(relations, gameState, family, economy, households, personality, random));

        FamilyRelationActions.Register(
            actions, gameState, family, relations, households, economy, locations, career, marriage, personality, random, events);
        LegacyFamilyRelationActions.Register(actions);

        context.GetService<IThoughtProviderRegistry>()?
            .Register(new FamilyRelationThoughtProvider(relations));

        context.Log("Family Relations mechanics registered.");
    }
}
