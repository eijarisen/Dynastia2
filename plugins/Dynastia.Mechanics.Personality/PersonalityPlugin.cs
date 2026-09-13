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

        var actions =
            context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException(
                "Action registry is unavailable.");

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Game random service is unavailable.");

        var economy =
            context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException(
                "Economy service is unavailable.");

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

        systems.Register(
            new MoralsDeteriorationYearSystem(
                personality,
                economy,
                random,
                events));

        actions.Register(
            new GameActionDefinition
            {
                Id = "personality.religious_study",
                Label = "Religious Study",
                Description = "Spend the year in deliberate religious or moral reflection. About a 50% chance to improve Morals; a Good person instead gains protection against the next downward Morals shift.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.MoralsReflection,
                IsAvailable = actionContext =>
                    actionContext.Actor.Id == actionContext.Target.Id
                    && actionContext.Actor.Tags.Has("state.alive")
                    && actionContext.Actor.Tags.Has("control.playable")
                    && actionContext.Actor.Age >= 18
                    && !actionContext.Actor.Tags.Has("state.imprisoned"),
                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    if (random.NextDouble() >= 0.50)
                    {
                        events.Publish(new GameEvent
                        {
                            Type = "personality.religious_study",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["success"] = "false",
                                ["text"] = $"{family.GetDisplayName(actor)} devoted time to religious study, but their outlook did not change."
                            }
                        });
                        return new GameActionResult(true);
                    }

                    var before = personality.GetPersonality(actor)?.Morals;
                    if (before == "Good")
                        personality.GrantMoralsProtection(actor);
                    else
                        personality.ShiftMorals(actor, 1);

                    var after = personality.GetPersonality(actor)?.Morals;
                    events.Publish(new GameEvent
                    {
                        Type = "personality.religious_study",
                        Year = actionContext.GameState.Year,
                        SubjectId = actor.Id,
                        Data = new Dictionary<string, string>
                        {
                            ["success"] = "true",
                            ["from"] = before ?? string.Empty,
                            ["to"] = after ?? string.Empty,
                            ["text"] = before == "Good"
                                ? $"{family.GetDisplayName(actor)} deepened their religious convictions."
                                : $"{family.GetDisplayName(actor)} emerged from religious study with a more benevolent outlook."
                        }
                    });
                    return new GameActionResult(true);
                }
            });

        context.Log(
            "Personality mechanics registered.");
    }
}
