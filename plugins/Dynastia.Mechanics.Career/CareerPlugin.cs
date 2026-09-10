using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class CareerPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var gameState = context.GetService<IGameState>()
            ?? throw new InvalidOperationException("Game state is unavailable.");

        var family = context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException("Family service is unavailable.");

        var stats = context.GetService<IStatsService>()
            ?? throw new InvalidOperationException("Stats service is unavailable.");

        var education = context.GetService<IEducationService>()
            ?? throw new InvalidOperationException("Education service is unavailable.");

        var health = context.GetService<IHealthService>()
            ?? throw new InvalidOperationException("Health service is unavailable.");

        var incomeRegistry = context.GetService<IIncomeProviderRegistry>()
            ?? throw new InvalidOperationException("Income provider registry is unavailable.");

        var healthModifiers = context.GetService<IAnnualHealthModifierRegistry>()
            ?? throw new InvalidOperationException("Health modifier registry is unavailable.");

        var random = context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException("Game random service is unavailable.");

        var events = context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException("Game event bus is unavailable.");

        var actions = context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException("Action registry is unavailable.");

        var systems = context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException("Year system registry is unavailable.");

        var career = new StandardCareerService(
            family,
            random);

        context.AddService<ICareerService>(career);

        InitializeFromEvents(
            gameState,
            career,
            random,
            events);

        incomeRegistry.Register(
            new CareerIncomeProvider(career));

        healthModifiers.Register(
            new CareerHealthModifierProvider(career));

        RegisterActions(
            actions,
            career,
            stats,
            random,
            family,
            events);

        RegisterFamilySupportActions(
            actions,
            career,
            stats,
            health,
            random,
            family,
            events);

        systems.Register(
            new CareerRetirementYearSystem(
                career,
                family,
                events));

        systems.Register(
            new CareerAdvancementYearSystem(
                career,
                education,
                stats,
                random,
                family,
                events));

        context.Log("Career mechanics registered.");
    }

    private static void InitializeFromEvents(
        IGameState gameState,
        ICareerService career,
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
                    {
                        career.InitializeCareer(
                            person,
                            person.Age >= 18
                                ? random.NextInt(0, 3)
                                : 0,
                            random.NextInt(1, 5));
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
                        career.InitializeCareer(
                            spouse,
                            random.NextInt(0, 3),
                            random.NextInt(1, 5));
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
                    {
                        career.InitializeCareer(
                            child,
                            0,
                            random.NextInt(1, 5));
                    }
                }
            };
    }

    private static void RegisterActions(
        IActionRegistry actions,
        ICareerService career,
        IStatsService stats,
        IGameRandom random,
        IFamilyService family,
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id = "career.quit_job",
                Label = "Quit the Job",
                Description =
                    "Leave employment. Because this resolves after finances, this year's salary is still paid.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.LifeEvents,

                IsAvailable = actionContext =>
                {
                    if (actionContext.Actor.Id != actionContext.Target.Id
                        || !actionContext.Actor.Tags.Has("state.alive")
                        || !actionContext.Actor.Tags.Has("control.playable"))
                    {
                        return false;
                    }

                    var current =
                        career.GetCareer(actionContext.Actor);

                    return !current.IsRetired
                        && current.JobLevel > 0;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var current = career.GetCareer(actor);

                    if (current.IsRetired
                        || current.JobLevel <= 0)
                    {
                        return new GameActionResult(false);
                    }

                    career.SetJobLevel(actor, 0);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "career.quit",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["text"] =
                                    $"{family.GetDisplayName(actor)} quit their job."
                            }
                        });

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "career.work_harder",
                Label = "Work Harder",
                Description =
                    "Increase this year's promotion chance, but lose 5 health during annual health processing.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    if (actionContext.Actor.Id != actionContext.Target.Id
                        || !actionContext.Actor.Tags.Has("state.alive")
                        || !actionContext.Actor.Tags.Has("control.playable")
                        || actionContext.Actor.Age < 18)
                    {
                        return false;
                    }

                    var current =
                        career.GetCareer(actionContext.Actor);

                    return !current.IsRetired
                        && current.JobLevel > 0
                        && current.JobLevel < 5;
                },

                Execute = actionContext =>
                {
                    actionContext.Actor.Tags.Add(
                        "modifier.work_harder");

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "career.seek_employment",
                Label = "Seek Employment",
                Description =
                    "Look for a basic job. Success depends on Strength. A successful job begins paying next year.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.LifeEvents,

                IsAvailable = actionContext =>
                {
                    if (actionContext.Actor.Id != actionContext.Target.Id
                        || !actionContext.Actor.Tags.Has("state.alive")
                        || !actionContext.Actor.Tags.Has("control.playable")
                        || actionContext.Actor.Age < 18)
                    {
                        return false;
                    }

                    var current =
                        career.GetCareer(actionContext.Actor);

                    return !current.IsRetired
                        && current.JobLevel == 0;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var current = career.GetCareer(actor);

                    if (current.IsRetired
                        || current.JobLevel != 0)
                    {
                        return new GameActionResult(false);
                    }

                    var strength = stats.GetStats(actor)
                        .First(stat =>
                            stat.Id.Equals(
                                "strength",
                                StringComparison.OrdinalIgnoreCase))
                        .Value;

                    var chance =
                        (strength / 5.0) * 0.5;

                    if (random.NextDouble() < chance)
                    {
                        career.SetJobLevel(actor, 1);

                        events.Publish(
                            new GameEvent
                            {
                                Type = "career.employment",
                                Year = actionContext.GameState.Year,
                                SubjectId = actor.Id,
                                Data = new Dictionary<string, string>
                                {
                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} found employment as a Laborer."
                                }
                            });
                    }

                    return new GameActionResult(true);
                }
            });
    }


    private static void RegisterFamilySupportActions(
        IActionRegistry actions,
        ICareerService career,
        IStatsService stats,
        IHealthService health,
        IGameRandom random,
        IFamilyService family,
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id = "career.help_seek_employment",
                Label = "Help Selected Relative Seek Employment",
                Description =
                    "Help your unemployed wife or unmarried adult daughter find a basic job. " +
                    "Success depends on her Strength.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || family.GetSex(target) != Sex.Female
                        || target.Age < 18)
                    {
                        return false;
                    }

                    var targetCareer =
                        career.GetCareer(target);

                    if (targetCareer.IsRetired
                        || targetCareer.JobLevel != 0)
                    {
                        return false;
                    }

                    var isWife =
                        family.GetSpouse(actor)?.Id == target.Id;

                    var isUnmarriedDaughter =
                        family.GetChildren(actor)
                            .Any(child => child.Id == target.Id)
                        && family.GetSpouse(target) is null;

                    return isWife
                        || isUnmarriedDaughter;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || family.GetSex(target) != Sex.Female
                        || target.Age < 18)
                    {
                        return new GameActionResult(false);
                    }

                    var targetCareer =
                        career.GetCareer(target);

                    var isWife =
                        family.GetSpouse(actor)?.Id == target.Id;

                    var isUnmarriedDaughter =
                        family.GetChildren(actor)
                            .Any(child => child.Id == target.Id)
                        && family.GetSpouse(target) is null;

                    if ((!isWife && !isUnmarriedDaughter)
                        || targetCareer.IsRetired
                        || targetCareer.JobLevel != 0)
                    {
                        return new GameActionResult(false);
                    }

                    var strength =
                        stats.GetStats(target)
                            .First(stat =>
                                stat.Id.Equals(
                                    "strength",
                                    StringComparison.OrdinalIgnoreCase))
                            .Value;

                    var chance =
                        (strength / 5.0) * 0.5;

                    if (random.NextDouble() < chance)
                    {
                        career.SetJobLevel(
                            target,
                            1);

                        events.Publish(
                            new GameEvent
                            {
                                Type = "career.employment",
                                Year = actionContext.GameState.Year,
                                SubjectId = target.Id,
                                RelatedPersonIds = [actor.Id],
                                Data = new Dictionary<string, string>
                                {
                                    ["text"] =
                                        $"{family.GetDisplayName(target)} found employment as a Laborer."
                                }
                            });
                    }

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "career.ask_to_recover",
                Label = "Ask Selected Relative to Recover",
                Description =
                    "Ask your miserable employed spouse or adult daughter to take a break. " +
                    "There is a 50% refusal chance.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id)
                    {
                        return false;
                    }

                    if (!IsRecoverOrQuitTarget(
                        actor,
                        target,
                        career,
                        family))
                    {
                        return false;
                    }

                    var targetCareer =
                        career.GetCareer(target);

                    return targetCareer.JobLevel > 0
                        && targetCareer.JobSatisfaction == 1;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || !IsRecoverOrQuitTarget(
                            actor,
                            target,
                            career,
                            family))
                    {
                        return new GameActionResult(false);
                    }

                    var targetCareer =
                        career.GetCareer(target);

                    if (targetCareer.JobLevel <= 0
                        || targetCareer.JobSatisfaction != 1)
                    {
                        return new GameActionResult(false);
                    }

                    if (random.NextDouble() > 0.5)
                    {
                        career.ChangeJobSatisfaction(
                            target,
                            2);

                        // Source behavior: immediate +20 may temporarily
                        // exceed max health; annual health later caps it.
                        health.ChangeHealthUnclamped(
                            target,
                            20);

                        events.Publish(
                            new GameEvent
                            {
                                Type = "career.ask_recover_success",
                                Year = actionContext.GameState.Year,
                                SubjectId = actor.Id,
                                RelatedPersonIds = [target.Id],
                                Data = new Dictionary<string, string>
                                {
                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} convinced " +
                                        $"{family.GetDisplayName(target)} to take a year off to recover."
                                }
                            });
                    }
                    else
                    {
                        events.Publish(
                            new GameEvent
                            {
                                Type = "career.ask_recover_failure",
                                Year = actionContext.GameState.Year,
                                SubjectId = actor.Id,
                                RelatedPersonIds = [target.Id],
                                Data = new Dictionary<string, string>
                                {
                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} asked " +
                                        $"{family.GetDisplayName(target)} to take a break, but they refused."
                                }
                            });
                    }

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "career.ask_to_quit",
                Label = "Ask Selected Relative to Quit Job",
                Description =
                    "Ask your miserable employed spouse or adult daughter to quit. " +
                    "There is a 50% refusal chance.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || !IsRecoverOrQuitTarget(
                            actor,
                            target,
                            career,
                            family))
                    {
                        return false;
                    }

                    var targetCareer =
                        career.GetCareer(target);

                    return targetCareer.JobLevel > 0
                        && targetCareer.JobSatisfaction == 1;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || !IsRecoverOrQuitTarget(
                            actor,
                            target,
                            career,
                            family))
                    {
                        return new GameActionResult(false);
                    }

                    var targetCareer =
                        career.GetCareer(target);

                    if (targetCareer.JobLevel <= 0
                        || targetCareer.JobSatisfaction != 1)
                    {
                        return new GameActionResult(false);
                    }

                    if (random.NextDouble() > 0.5)
                    {
                        career.SetJobLevel(
                            target,
                            0);

                        events.Publish(
                            new GameEvent
                            {
                                Type = "career.ask_quit_success",
                                Year = actionContext.GameState.Year,
                                SubjectId = actor.Id,
                                RelatedPersonIds = [target.Id],
                                Data = new Dictionary<string, string>
                                {
                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} asked " +
                                        $"{family.GetDisplayName(target)} to quit their job, and they agreed."
                                }
                            });
                    }
                    else
                    {
                        events.Publish(
                            new GameEvent
                            {
                                Type = "career.ask_quit_failure",
                                Year = actionContext.GameState.Year,
                                SubjectId = actor.Id,
                                RelatedPersonIds = [target.Id],
                                Data = new Dictionary<string, string>
                                {
                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} asked " +
                                        $"{family.GetDisplayName(target)} to quit their job, but they refused."
                                }
                            });
                    }

                    return new GameActionResult(true);
                }
            });
    }

    private static bool CanActorSupport(
        IPerson actor)
    {
        return actor.Tags.Has("state.alive")
            && actor.Tags.Has("control.playable")
            && !actor.Tags.Has("state.imprisoned");
    }

    private static bool IsRecoverOrQuitTarget(
        IPerson actor,
        IPerson target,
        ICareerService career,
        IFamilyService family)
    {
        if (family.GetSpouse(actor)?.Id == target.Id)
            return true;

        return family.GetChildren(actor)
            .Any(child => child.Id == target.Id)
            && family.GetSex(target) == Sex.Female
            && target.Age >= 18;
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
