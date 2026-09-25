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

        var households =
            context.GetService<IHouseholdService>()
            ?? throw new InvalidOperationException(
                "Household service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var eventPresentation =
            context.GetService<IEventPresentationRegistry>()
            ?? throw new InvalidOperationException(
                "Event presentation registry is unavailable.");

        var biography =
            new StandardBiographyService(
                gameState,
                family,
                stats,
                locations,
                households,
                events,
                eventPresentation);

        context.AddService<IBiographyService>(
            biography);

        context.GetService<IStateReconciliationLifecycle>()?
            .Register(
                "biography.generated_adult_milestones",
                Enum.GetValues<ReconciliationLifecycleStage>(),
                _ => biography.ReconcileGeneratedAdultLifeMilestones(),
                order: 95);

        context.Log(
            "Biography mechanics registered.");
    }
}
