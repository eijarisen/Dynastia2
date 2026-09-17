using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class HealthPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var data = context.GetService<IGameDataService>() ?? throw new InvalidOperationException("Game data service is unavailable.");
        var random = context.GetService<IGameRandom>() ?? throw new InvalidOperationException("Game random service is unavailable.");
        var state = context.GetService<IGameState>() ?? throw new InvalidOperationException("Game state is unavailable.");
        var stats = context.GetService<IStatsService>() ?? throw new InvalidOperationException("Stats service is unavailable. The Health plugin requires dynastia.stats.");
        var family = context.GetService<IFamilyService>() ?? throw new InvalidOperationException("Family service is unavailable. The Health plugin requires dynastia.family.");
        var economy = context.GetService<IEconomyService>() ?? throw new InvalidOperationException("Economy service is unavailable. The Health plugin requires dynastia.economy.");
        var locations = context.GetService<IExistingLocationService>() ?? throw new InvalidOperationException("Existing-location service is unavailable. The Health plugin requires dynastia.locations.");
        var contextWeights = context.GetService<IContextWeightService>() ?? throw new InvalidOperationException("Context-weight service is unavailable.");
        var events = context.GetService<IGameEventBus>() ?? throw new InvalidOperationException("Game event bus is unavailable.");
        var systems = context.GetService<IYearSystemRegistry>() ?? throw new InvalidOperationException("Year system registry is unavailable.");

        var modifiers = new AnnualHealthModifierRegistry();
        context.AddService<IAnnualHealthModifierRegistry>(modifiers);

        var health = new StandardHealthService(data, random);
        health.ConfigureHistoricalCatalog(
            HistoricalHealthCatalog.Load(data, health.ConditionIds));
        context.AddService<IHealthService>(health);

        var healthContext = contextWeights.LoadCatalog(
            "Health/health_condition_context_weights.csv",
            health.ConditionIds);

        var stressOutcomes = StressOutcomeCatalog.Load(data, health.Definitions);
        var stressModifiers = new StressModifierRegistry();
        context.AddService<IStressModifierRegistry>(stressModifiers);

        var stress = new StandardStressService(
            state,
            family,
            economy,
            health,
            events,
            stressModifiers);
        context.AddService<IStressService>(stress);

        var genetics = new GeneticPredispositionService(state, family);

        var reconciliation = context.GetService<IStateReconciliationLifecycle>()
            ?? throw new InvalidOperationException(
                "State reconciliation lifecycle is unavailable.");

        reconciliation.Register(
            "health.components",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad,
                ReconciliationLifecycleStage.BeforeYear,
                ReconciliationLifecycleStage.AfterYear,
                ReconciliationLifecycleStage.AfterImmediateAction,
                ReconciliationLifecycleStage.AfterQueuedAction,
                ReconciliationLifecycleStage.AfterPersonCreated
            ],
            _ => health.ReconcileAll(state.People),
            order: 40);

        reconciliation.Register(
            "health.genetic_predispositions",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad
            ],
            _ => genetics.ReconcileAll(),
            order: 45);

        systems.Register(new GeneticPredispositionYearSystem(genetics));
        systems.Register(new HealthYearSystem(
            health,
            stats,
            family,
            locations,
            healthContext,
            random,
            events,
            modifiers));
        systems.Register(new MentalHealthYearSystem(
            health,
            family,
            locations,
            stress,
            healthContext,
            stressOutcomes,
            random,
            events));

        _ = new HealthInjuryEventTracker(state, health, random, events);

        context.Log("Health mechanics registered.");
    }
}
