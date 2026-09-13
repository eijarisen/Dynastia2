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
        var events = context.GetService<IGameEventBus>() ?? throw new InvalidOperationException("Game event bus is unavailable.");
        var systems = context.GetService<IYearSystemRegistry>() ?? throw new InvalidOperationException("Year system registry is unavailable.");

        var modifiers = new AnnualHealthModifierRegistry();
        context.AddService<IAnnualHealthModifierRegistry>(modifiers);

        var health = new StandardHealthService(data, random);
        context.AddService<IHealthService>(health);

        var genetics = new GeneticPredispositionService(state, family);
        genetics.ReconcileAll();
        systems.Register(new GeneticPredispositionYearSystem(genetics));
        systems.Register(new HealthYearSystem(health, stats, family, random, events, modifiers));
        systems.Register(new MentalHealthYearSystem(health, family, economy, random, events));

        _ = new HealthInjuryEventTracker(state, health, random, events);

        context.Log("Health mechanics registered.");
    }
}
