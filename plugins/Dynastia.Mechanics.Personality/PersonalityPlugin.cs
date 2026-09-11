using Dynastia.Contracts;

namespace Dynastia.Mechanics.Personality;

public sealed class PersonalityPlugin :
    IGamePlugin
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

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        var personality =
            new StandardPersonalityService(
                gameState,
                family);

        context.AddService<IPersonalityService>(
            personality);

        personality.ReconcileAll();

        events.EventPublished +=
            (_, gameEvent) =>
            {
                if (gameEvent.Type.Equals(
                        "game.started",
                        StringComparison.OrdinalIgnoreCase))
                {
                    personality.ReconcileAll();
                    return;
                }

                if (!gameEvent.Type.Equals(
                        "relationship.married",
                        StringComparison.OrdinalIgnoreCase)
                    && !gameEvent.Type.Equals(
                        "relationship.remarried",
                        StringComparison.OrdinalIgnoreCase)
                    && !gameEvent.Type.Equals(
                        "relationship.partnered",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                foreach (var id in
                    gameEvent.RelatedPersonIds)
                {
                    var person =
                        gameState.People
                            .FirstOrDefault(
                                candidate =>
                                    candidate.Id
                                    == id);

                    if (person is not null)
                    {
                        personality.ReconcilePerson(
                            person);
                    }
                }
            };

        systems.Register(
            new PersonalityAssignmentYearSystem(
                personality));

        systems.Register(
            new PersonalityPostYearSystem(
                personality));

        context.Log(
            "Personality mechanics registered.");
    }
}
