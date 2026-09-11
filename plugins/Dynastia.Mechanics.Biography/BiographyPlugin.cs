using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed class BiographyPlugin : IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        var gameState =
            context.GetService<IGameState>()
            ?? throw new InvalidOperationException(
                "Game state is unavailable.");

        var family =
            context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var stats =
            context.GetService<IStatsService>()
            ?? throw new InvalidOperationException(
                "Stats service is unavailable.");

        var locations =
            context.GetService<ILocationService>()
            ?? throw new InvalidOperationException(
                "Location service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var biography =
            new StandardBiographyService(
                gameState,
                family,
                stats,
                locations,
                events);

        context.AddService<IBiographyService>(
            biography);

        context.Log(
            "Biography mechanics registered.");
    }
}
