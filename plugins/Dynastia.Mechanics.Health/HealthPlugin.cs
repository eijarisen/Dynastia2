using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class HealthPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var data =
            context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException(
                "Game data service is unavailable.");

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Game random service is unavailable.");

        var stats =
            context.GetService<IStatsService>()
            ?? throw new InvalidOperationException(
                "Stats service is unavailable. " +
                "The Health plugin requires dynastia.stats.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        var healthService =
            new StandardHealthService(
                data,
                random);

        context.AddService<IHealthService>(
            healthService);

        systems.Register(
            new HealthYearSystem(
                healthService,
                stats,
                random,
                events));

        context.Log(
            "Health mechanics registered.");
    }
}
