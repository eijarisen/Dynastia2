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

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Game random service is unavailable.");

        var actions =
            context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException(
                "Action registry is unavailable.");

        var personality =
            new StandardPersonalityService(
                gameState,
                family);

        context.AddService<IPersonalityService>(
            personality);

        context.AddService<IMoralsDevelopmentService>(
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
            new MoralsDeteriorationYearSystem(
                personality,
                random,
                events));

        systems.Register(
            new PersonalityPostYearSystem(
                personality));

        actions.Register(
            new GameActionDefinition
            {
                Id = "personality.religious_study",
                Label = "Religious Study",
                Description =
                    "Spend the year in deliberate moral and religious reflection. Success is about 50%; Evil may become Neutral, Neutral may become Good, while Good gains protection against the next downward Morals shift.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.PostYear,
                IsAvailable = actionContext =>
                    actionContext.Actor.Id == actionContext.Target.Id
                    && actionContext.Actor.Tags.Has("state.alive")
                    && actionContext.Actor.Tags.Has("control.playable")
                    && actionContext.Actor.Age >= 18
                    && !actionContext.Actor.Tags.Has("state.imprisoned"),
                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var before = personality.GetPersonality(actor);
                    if (before is null)
                        return new GameActionResult(false);

                    var success = random.NextDouble() < 0.50;
                    var changed = false;

                    if (success)
                    {
                        if (before.Morals.Equals("Good", StringComparison.OrdinalIgnoreCase))
                        {
                            ((IMoralsDevelopmentService)personality)
                                .GrantMoralsProtection(actor);
                        }
                        else
                        {
                            changed = personality.ShiftMorals(actor, 1);
                        }
                    }

                    var after = personality.GetPersonality(actor);

                    events.Publish(
                        new GameEvent
                        {
                            Type = success
                                ? "personality.religious_study_success"
                                : "personality.religious_study_failure",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["text"] = success
                                    ? changed
                                        ? $"{family.GetDisplayName(actor)}'s religious study changed their moral outlook from {before.Morals} to {after?.Morals}."
                                        : $"{family.GetDisplayName(actor)}'s religious study strengthened their moral resolve."
                                    : $"{family.GetDisplayName(actor)} devoted the year to religious study, but their moral outlook did not change."
                            }
                        });

                    return new GameActionResult(true);
                }
            });

        context.Log(
            "Personality mechanics registered.");
    }
}
