using Dynastia.Contracts;

namespace Dynastia.Mechanics.Education;

public sealed class EducationPlugin : IGamePlugin
{
    private const decimal EducationCost = 2000m;
    private const double BaseSuccessChance = 0.30;
    private const double IntellectMultiplier = 0.15;

    private const int HelpLearningMinimumAge = 6;
    private const int HelpLearningAdultAge = 18;
    private const double HelpLearningBaseChance = 0.10;
    private const double HelpLearningEducationStep = 0.15;

    public void Initialize(IGamePluginContext context)
    {
        var gameState = context.GetService<IGameState>()
            ?? throw new InvalidOperationException("Game state is unavailable.");

        var family = context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException("Family service is unavailable.");

        var stats = context.GetService<IStatsService>()
            ?? throw new InvalidOperationException("Stats service is unavailable.");

        var health = context.GetService<IHealthService>()
            ?? throw new InvalidOperationException("Health service is unavailable.");

        var economy = context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException("Economy service is unavailable.");

        var random = context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException("Game random service is unavailable.");

        var events = context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException("Game event bus is unavailable.");

        var systems = context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException("Year system registry is unavailable.");

        var actions = context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException("Action registry is unavailable.");

        var education = new StandardEducationService();

        context.AddService<IEducationService>(education);

        InitializeFromEvents(
            gameState,
            education,
            random,
            events);

        actions.Register(
            CreateEducationAction(
                education,
                family,
                stats,
                economy,
                random,
                events));

        actions.Register(
            CreateHelpLearningAction(
                education,
                family,
                random,
                events));

        systems.Register(
            new PassiveEducationYearSystem(
                education,
                stats,
                health,
                random));

        context.Log("Education mechanics registered.");
    }

    private static void InitializeFromEvents(
        IGameState gameState,
        IEducationService education,
        IGameRandom random,
        IGameEventBus events)
    {
        events.EventPublished +=
            (_, gameEvent) =>
            {
                if (gameEvent.Type.Equals(
                    "game.started",
                    StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var person in gameState.People)
                        education.SetEducationLevel(person, 0);

                    if (gameEvent.SubjectId is Guid founderId)
                    {
                        var founder =
                            gameState.People.FirstOrDefault(
                                person => person.Id == founderId);

                        if (founder is not null)
                        {
                            education.SetEducationLevel(
                                founder,
                                random.NextInt(1, 2));
                        }
                    }

                    return;
                }

                if (gameEvent.Type.Equals(
                        "relationship.married",
                        StringComparison.OrdinalIgnoreCase)
                    || gameEvent.Type.Equals(
                        "relationship.partnered",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var spouse =
                        FindFirstRelatedPerson(
                            gameState,
                            gameEvent);

                    if (spouse is not null)
                    {
                        education.SetEducationLevel(
                            spouse,
                            random.NextInt(1, 3));
                    }

                    return;
                }

                if (gameEvent.Type.Equals(
                        "life.birth",
                        StringComparison.OrdinalIgnoreCase)
                    && gameEvent.SubjectId is Guid childId)
                {
                    var child =
                        gameState.People.FirstOrDefault(
                            person => person.Id == childId);

                    if (child is not null)
                        education.SetEducationLevel(child, 0);
                }
            };
    }

    private static GameActionDefinition CreateEducationAction(
        IEducationService education,
        IFamilyService family,
        IStatsService stats,
        IEconomyService economy,
        IGameRandom random,
        IGameEventBus events)
    {
        return new GameActionDefinition
        {
            Id = "education.get_education",
            Label = "Get Education ($2,000)",
            Description =
                "Pay for a course. The cost is paid whether the course succeeds or fails. " +
                "Success depends on the target's Intellect.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,

            IsAvailable = actionContext =>
            {
                var actor = actionContext.Actor;
                var target = actionContext.Target;

                if (!actor.Tags.Has("state.alive")
                    || !actor.Tags.Has("control.playable")
                    || !target.Tags.Has("state.alive"))
                {
                    return false;
                }

                var spouse = family.GetSpouse(actor);

                var validTarget =
                    target.Id == actor.Id
                    || spouse?.Id == target.Id;

                if (!validTarget
                    || education.GetEducationLevel(target) >= 5)
                {
                    return false;
                }

                var household = economy.GetHousehold(actor);

                return household is not null
                    && household.Wealth >= EducationCost;
            },

            Execute = actionContext =>
            {
                var actor = actionContext.Actor;
                var target = actionContext.Target;

                var household = economy.GetHousehold(actor);

                if (household is null
                    || household.Wealth < EducationCost
                    || education.GetEducationLevel(target) >= 5)
                {
                    return new GameActionResult(
                        false,
                        "Education is no longer available.");
                }

                economy.ChangeWealth(
                    actor,
                    -EducationCost);

                var intellect = stats.GetStats(target)
                    .First(stat =>
                        stat.Id.Equals(
                            "intellect",
                            StringComparison.OrdinalIgnoreCase))
                    .Value;

                var successChance =
                    BaseSuccessChance
                    + intellect * IntellectMultiplier;

                var success =
                    random.NextDouble() < successChance;

                if (success)
                {
                    education.IncreaseEducation(target);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "education.success",
                            Year = actionContext.GameState.Year,
                            SubjectId = target.Id,
                            RelatedPersonIds =
                                actor.Id == target.Id
                                    ? []
                                    : [actor.Id],
                            Data = new Dictionary<string, string>
                            {
                                ["level"] =
                                    education.GetEducationLevel(target).ToString(),
                                ["text"] =
                                    $"{family.GetDisplayName(target)} successfully completed a course, " +
                                    $"reaching education level {education.GetEducationLevel(target)}."
                            }
                        });
                }
                else
                {
                    events.Publish(
                        new GameEvent
                        {
                            Type = "education.failure",
                            Year = actionContext.GameState.Year,
                            SubjectId = target.Id,
                            RelatedPersonIds =
                                actor.Id == target.Id
                                    ? []
                                    : [actor.Id],
                            Data = new Dictionary<string, string>
                            {
                                ["text"] =
                                    $"{family.GetDisplayName(target)} attempted to further " +
                                    "their education but failed the course."
                            }
                        });
                }

                return new GameActionResult(true);
            }
        };
    }

    private static GameActionDefinition CreateHelpLearningAction(
        IEducationService education,
        IFamilyService family,
        IGameRandom random,
        IGameEventBus events)
    {
        return new GameActionDefinition
        {
            Id =
                "education.help_learning",

            Label =
                "Help in Learning",

            Description =
                "Spend the year helping the selected child study. " +
                "Available from age 6 through 17. Success depends on " +
                "the father's Education level: 10%, 25%, 40%, 55%, " +
                "70% or 85% at levels 0-5.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.QueuedActionsEarly,

            IsAvailable =
                actionContext =>
                {
                    var father =
                        actionContext.Actor;

                    var child =
                        actionContext.Target;

                    if (!father.Tags.Has(
                            "state.alive")
                        || !father.Tags.Has(
                            "control.playable")
                        || !child.Tags.Has(
                            "state.alive")
                        || child.Id == father.Id
                        || child.Age < HelpLearningMinimumAge
                        || child.Age >= HelpLearningAdultAge
                        || education.GetEducationLevel(
                            child) >= 5)
                    {
                        return false;
                    }

                    return family
                        .GetChildren(
                            father)
                        .Any(
                            candidate =>
                                candidate.Id
                                == child.Id);
                },

            Execute =
                actionContext =>
                {
                    var father =
                        actionContext.Actor;

                    var child =
                        actionContext.Target;

                    if (!father.Tags.Has(
                            "state.alive")
                        || !child.Tags.Has(
                            "state.alive")
                        || child.Age < HelpLearningMinimumAge
                        || child.Age >= HelpLearningAdultAge
                        || education.GetEducationLevel(
                            child) >= 5
                        || !family
                            .GetChildren(
                                father)
                            .Any(
                                candidate =>
                                    candidate.Id
                                    == child.Id))
                    {
                        return new GameActionResult(
                            false,
                            "Help in Learning is no longer available.");
                    }

                    var fatherEducation =
                        education.GetEducationLevel(
                            father);

                    var successChance =
                        HelpLearningBaseChance
                        + fatherEducation
                            * HelpLearningEducationStep;

                    var success =
                        random.NextDouble()
                        < successChance;

                    if (success)
                    {
                        education.IncreaseEducation(
                            child);

                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "education.help_learning_success",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    child.Id,

                                RelatedPersonIds =
                                    [father.Id],

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["level"] =
                                            education
                                                .GetEducationLevel(
                                                    child)
                                                .ToString(),

                                        ["chance"] =
                                            successChance
                                                .ToString(
                                                    "0.00"),

                                        ["text"] =
                                            $"{family.GetDisplayName(father)} " +
                                            $"helped {family.GetDisplayName(child)} " +
                                            "with their studies, raising the child's " +
                                            $"Education to level " +
                                            $"{education.GetEducationLevel(child)}."
                                    }
                            });
                    }
                    else
                    {
                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "education.help_learning_failure",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    child.Id,

                                RelatedPersonIds =
                                    [father.Id],

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["chance"] =
                                            successChance
                                                .ToString(
                                                    "0.00"),

                                        ["text"] =
                                            $"{family.GetDisplayName(father)} " +
                                            $"spent time helping " +
                                            $"{family.GetDisplayName(child)} study, " +
                                            "but the child's Education did not improve."
                                    }
                            });
                    }

                    return new GameActionResult(
                        true);
                }
        };
    }

    private static IPerson? FindFirstRelatedPerson(
        IGameState gameState,
        GameEvent gameEvent)
    {
        if (gameEvent.RelatedPersonIds.Count == 0)
            return null;

        var id = gameEvent.RelatedPersonIds[0];

        return gameState.People.FirstOrDefault(
            person => person.Id == id);
    }
}
