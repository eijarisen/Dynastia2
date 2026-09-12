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

        var data = context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException("Game data service is unavailable.");

        var localOpportunities =
            context.GetService<ILocalCareerOpportunityService>()
            ?? throw new InvalidOperationException(
                "Local career opportunity service is unavailable.");

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

        var catalog =
            CareerCatalog.Load(
                data);

        var career =
            new StandardCareerService(
                gameState,
                family,
                random,
                catalog,
                localOpportunities);

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
                        var initialJobLevel =
                            gameEvent.SubjectId is Guid founderId
                            && person.Id == founderId
                                ? 1
                                : person.Age >= 18
                                    ? random.NextInt(0, 3)
                                    : 0;

                        career.InitializeCareer(
                            person,
                            initialJobLevel,
                            random.NextInt(1, 5));
                    }

                    return;
                }

                if (gameEvent.Type.Equals(
                        "relationship.married",
                        StringComparison.OrdinalIgnoreCase)
                    || gameEvent.Type.Equals(
                        "relationship.partnered",
                        StringComparison.OrdinalIgnoreCase)
                    || gameEvent.Type.Equals(
                        "relationship.remarried",
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
        StandardCareerService career,
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
                                ["careerId"] =
                                    current.CareerId
                                    ?? string.Empty,

                                ["careerName"] =
                                    current.CareerName
                                    ?? string.Empty,

                                ["jobTitle"] =
                                    current.JobTitle,

                                ["text"] =
                                    $"{family.GetDisplayName(actor)} quit their job as {current.JobTitle}."
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
                    "Look for work. The game first draws a career from professions open in the current year. Manual/physical careers use Strength; office, professional and technical careers use Intellect. Strong regional or local opportunities also improve the chance of being hired. The new job begins paying next year.",
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

                    var opportunity =
                        career.CreateEmploymentOpportunity(
                            actor,
                            stats);

                    var successChance =
                        PersonalityInfluence.AdjustProbability(
                            opportunity.SuccessChance,
                            actor,
                            sanguine: 0.10);

                    if (random.NextDouble()
                        < successChance)
                    {
                        career.AcceptEmploymentOpportunity(
                            actor,
                            opportunity);

                        var employed =
                            career.GetCareer(
                                actor);

                        events.Publish(
                            new GameEvent
                            {
                                Type = "career.employment",
                                Year = actionContext.GameState.Year,
                                SubjectId = actor.Id,
                                Data = new Dictionary<string, string>
                                {
                                    ["careerId"] =
                                        employed.CareerId
                                        ?? string.Empty,

                                    ["careerName"] =
                                        employed.CareerName
                                        ?? string.Empty,

                                    ["jobTitle"] =
                                        employed.JobTitle,

                                    ["aptitudeStat"] =
                                        opportunity.StatId,

                                    ["aptitudeValue"] =
                                        opportunity.StatValue
                                            .ToString(),

                                    ["chance"] =
                                        successChance
                                            .ToString(
                                                "0.00"),

                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} found employment as {employed.JobTitle}."
                                }
                            });
                    }

                    return new GameActionResult(true);
                }
            });
    }


    private static void RegisterFamilySupportActions(
        IActionRegistry actions,
        StandardCareerService career,
        IStatsService stats,
        IHealthService health,
        IGameRandom random,
        IFamilyService family,
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id =
                    "career.use_family_connections",

                Label =
                    "Use Family Connections",

                Description =
                    "Ask a well-established parent to use their professional connections. " +
                    "A parent whose lifetime peak job level was 3 or higher may place " +
                    "the unemployed adult child in the same field two job levels below " +
                    "their own peak. The parent may refuse.",

                Mode =
                    ActionExecutionMode.Queued,

                QueuePhase =
                    YearPhase.LifeEvents,

                IsAvailable =
                    actionContext =>
                    {
                        var actor =
                            actionContext.Actor;

                        var target =
                            actionContext.Target;

                        if (!CanActorSupport(
                                actor)
                            || !target.Tags.Has(
                                "state.alive")
                            || target.Age < 18)
                        {
                            return false;
                        }

                        var actorIsTarget =
                            actor.Id
                            == target.Id;

                        var actorIsParent =
                            family.GetFather(
                                target)?.Id
                                == actor.Id
                            || family.GetMother(
                                target)?.Id
                                == actor.Id;

                        if (!actorIsTarget
                            && !actorIsParent)
                        {
                            return false;
                        }

                        var targetCareer =
                            career.GetCareer(
                                target);

                        return !targetCareer.IsRetired
                            && targetCareer.JobLevel == 0
                            && career.FindBestFamilyConnection(
                                target) is not null;
                    },

                Execute =
                    actionContext =>
                    {
                        var actor =
                            actionContext.Actor;

                        var target =
                            actionContext.Target;

                        var targetCareer =
                            career.GetCareer(
                                target);

                        var opportunity =
                            career.FindBestFamilyConnection(
                                target);

                        if (!CanActorSupport(
                                actor)
                            || !target.Tags.Has(
                                "state.alive")
                            || target.Age < 18
                            || targetCareer.IsRetired
                            || targetCareer.JobLevel != 0
                            || opportunity is null)
                        {
                            return new GameActionResult(
                                false);
                        }

                        var actorIsTarget =
                            actor.Id
                            == target.Id;

                        var actorIsParent =
                            family.GetFather(
                                target)?.Id
                                == actor.Id
                            || family.GetMother(
                                target)?.Id
                                == actor.Id;

                        if (!actorIsTarget
                            && !actorIsParent)
                        {
                            return new GameActionResult(
                                false);
                        }

                        if (random.NextDouble()
                            >= opportunity.AcceptanceChance)
                        {
                            events.Publish(
                                new GameEvent
                                {
                                    Type =
                                        "career.family_connections_failure",

                                    Year =
                                        actionContext.GameState.Year,

                                    SubjectId =
                                        target.Id,

                                    RelatedPersonIds =
                                        new[]
                                        {
                                            opportunity.Parent.Id,
                                            actor.Id
                                        }
                                        .Distinct()
                                        .ToList(),

                                    Data =
                                        new Dictionary<string, string>
                                        {
                                            ["parentPeakLevel"] =
                                                opportunity.ParentPeakLevel
                                                    .ToString(),

                                            ["careerId"] =
                                                opportunity.Career.Id,

                                            ["chance"] =
                                                opportunity.AcceptanceChance
                                                    .ToString(
                                                        "0.00"),

                                            ["text"] =
                                                $"{family.GetDisplayName(target)} " +
                                                $"asked {family.GetDisplayName(opportunity.Parent)} " +
                                                "to use family connections to find work, " +
                                                "but the parent refused."
                                        }
                                });

                            return new GameActionResult(
                                true);
                        }

                        career.AcceptFamilyConnection(
                            target,
                            opportunity);

                        var employed =
                            career.GetCareer(
                                target);

                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "career.family_connections_success",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    target.Id,

                                RelatedPersonIds =
                                    new[]
                                    {
                                        opportunity.Parent.Id,
                                        actor.Id
                                    }
                                    .Distinct()
                                    .ToList(),

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["parentPeakLevel"] =
                                            opportunity.ParentPeakLevel
                                                .ToString(),

                                        ["careerId"] =
                                            employed.CareerId
                                            ?? string.Empty,

                                        ["careerName"] =
                                            employed.CareerName
                                            ?? string.Empty,

                                        ["jobTitle"] =
                                            employed.JobTitle,

                                        ["jobLevel"] =
                                            employed.JobLevel
                                                .ToString(),

                                        ["text"] =
                                            $"{family.GetDisplayName(opportunity.Parent)} " +
                                            $"used family connections to secure " +
                                            $"{family.GetDisplayName(target)} a position as " +
                                            $"{employed.JobTitle}."
                                    }
                            });

                        return new GameActionResult(
                            true);
                    }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "career.help_seek_employment",
                Label = "Help to Seek Employment",
                Description =
                    "Help your unemployed wife or unmarried adult daughter find work. " +
                    "A career is drawn first; manual/physical careers use her Strength while office, professional and technical careers use her Intellect.",
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

                    var opportunity =
                        career.CreateEmploymentOpportunity(
                            target,
                            stats);

                    var successChance =
                        PersonalityInfluence.AdjustProbability(
                            opportunity.SuccessChance,
                            target,
                            sanguine: 0.10);

                    if (random.NextDouble()
                        < successChance)
                    {
                        career.AcceptEmploymentOpportunity(
                            target,
                            opportunity);

                        var employed =
                            career.GetCareer(
                                target);

                        events.Publish(
                            new GameEvent
                            {
                                Type = "career.employment",
                                Year = actionContext.GameState.Year,
                                SubjectId = target.Id,
                                RelatedPersonIds = [actor.Id],
                                Data = new Dictionary<string, string>
                                {
                                    ["careerId"] =
                                        employed.CareerId
                                        ?? string.Empty,

                                    ["careerName"] =
                                        employed.CareerName
                                        ?? string.Empty,

                                    ["jobTitle"] =
                                        employed.JobTitle,

                                    ["aptitudeStat"] =
                                        opportunity.StatId,

                                    ["aptitudeValue"] =
                                        opportunity.StatValue
                                            .ToString(),

                                    ["chance"] =
                                        successChance
                                            .ToString(
                                                "0.00"),

                                    ["text"] =
                                        $"{family.GetDisplayName(target)} found employment as {employed.JobTitle}."
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
                Label = "Ask to Recover",
                Description =
                    "Ask your miserable or unhappy employed spouse, or a miserable adult daughter, " +
                    "to take a break. There is a 50% refusal chance.",
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

                    var maximumSatisfaction =
                        family.GetSpouse(actor)?.Id == target.Id
                            ? 2
                            : 1;

                    return targetCareer.JobLevel > 0
                        && targetCareer.JobSatisfaction
                            <= maximumSatisfaction;
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

                    var maximumSatisfaction =
                        family.GetSpouse(actor)?.Id == target.Id
                            ? 2
                            : 1;

                    if (targetCareer.JobLevel <= 0
                        || targetCareer.JobSatisfaction
                            > maximumSatisfaction)
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
                Label = "Ask to Quit Job",
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
