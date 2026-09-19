using Dynastia.Contracts;

namespace Dynastia.Mechanics.Education;

public sealed class EducationPlugin : IGamePlugin
{
    private const decimal EducationCost = 3000m;
    private const int HelpLearningMinimumAge = 6;
    private const int HelpLearningAdultAge = 18;

    public void Initialize(IGamePluginContext context)
    {
        var gameState = context.GetService<IGameState>()
            ?? throw new InvalidOperationException("Game state is unavailable.");

        var data = context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException("Game data service is unavailable.");

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

        var eras = EducationEraCatalog.Load(data);

        var education = new StandardEducationService(
            family,
            eras);

        context.AddService<IEducationService>(education);

        var reconciliation = context.GetService<IStateReconciliationLifecycle>()
            ?? throw new InvalidOperationException(
                "State reconciliation lifecycle is unavailable.");

        reconciliation.Register(
            "education.components",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad,
                ReconciliationLifecycleStage.BeforeYear,
                ReconciliationLifecycleStage.AfterYear,
                ReconciliationLifecycleStage.AfterImmediateAction,
                ReconciliationLifecycleStage.AfterQueuedAction,
                ReconciliationLifecycleStage.AfterPersonCreated
            ],
            _ =>
            {
                foreach (var person in gameState.People)
                    education.ReconcilePerson(person);
            },
            order: 20);

        InitializeFromEvents(
            gameState,
            education,
            random,
            events,
            eras);

        actions.Register(
            CreateEducationAction(
                education,
                family,
                stats,
                economy,
                random,
                events,
                () => context.GetService<ICraftService>()));

        actions.Register(
            CreateHelpLearningAction(
                education,
                family,
                economy,
                stats,
                random,
                events,
                eras));

        systems.Register(
            new PassiveEducationYearSystem(
                education,
                stats,
                health,
                random,
                eras));

        context.Log("Education mechanics registered.");
    }

    private static void InitializeFromEvents(
        IGameState gameState,
        IEducationService education,
        IGameRandom random,
        IGameEventBus events,
        EducationEraCatalog eras)
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
                            var era = eras.GetRule(gameEvent.Year);

                            // The starting parents and older sister are all
                            // adults with a pre-game life history. Give each
                            // of them a historically appropriate adult
                            // education profile before Career initializes so
                            // starting job level and education can agree.
                            foreach (var adult in gameState.People.Where(
                                person =>
                                    person.Id != founder.Id
                                    && person.Age >= 18))
                            {
                                education.SetEducationLevel(
                                    adult,
                                    random.NextInt(
                                        era.GeneratedAdultMinLevel,
                                        era.GeneratedAdultMaxLevel));
                            }

                            education.SetEducationLevel(
                                founder,
                                random.NextInt(
                                    era.FounderMinLevel,
                                    era.FounderMaxLevel));
                        }
                    }

                    return;
                }

                if (gameEvent.Data.TryGetValue(
                        "preserveGeneratedProfile",
                        out var preserveGeneratedProfile)
                    && preserveGeneratedProfile.Equals(
                        "true",
                        StringComparison.OrdinalIgnoreCase)
                    && (gameEvent.Type.Equals(
                            "relationship.married",
                            StringComparison.OrdinalIgnoreCase)
                        || gameEvent.Type.Equals(
                            "relationship.partnered",
                            StringComparison.OrdinalIgnoreCase)))
                {
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
                        var era = eras.GetRule(gameEvent.Year);

                        education.SetEducationLevel(
                            spouse,
                            random.NextInt(
                                era.GeneratedAdultMinLevel,
                                era.GeneratedAdultMaxLevel));
                    }

                    return;
                }

                if (gameEvent.Type.Equals(
                        "household.nanny_hired",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var nanny =
                        FindFirstRelatedPerson(
                            gameState,
                            gameEvent);

                    if (nanny is not null)
                    {
                        var era = eras.GetRule(gameEvent.Year);

                        education.SetEducationLevel(
                            nanny,
                            random.NextInt(
                                era.GeneratedAdultMinLevel,
                                era.GeneratedAdultMaxLevel));
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
        IGameEventBus events,
        Func<ICraftService?> craftResolver)
    {
        return new GameActionDefinition
        {
            Id = "education.get_education",
            Label = "Get Education",
            Description =
                "Choose standard education or study a Craft. Every option costs 3,000 zł when the attempt is made.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,

            IsAvailable = actionContext =>
            {
                var actor = actionContext.Actor;
                var target = actionContext.Target;

                if (!actor.Tags.Has("state.alive")
                    || !actionContext.ActorHasControl
                    || !target.Tags.Has("state.alive")
                    || target.Age < 18)
                {
                    return false;
                }

                var validTarget = target.Id == actor.Id
                    || HouseholdKinshipRules.IsSupportedResidentRelative(
                        actor,
                        target,
                        family,
                        economy);
                if (!validTarget)
                    return false;

                if (!economy.CanAfford(actor, EducationCost))
                    return false;

                var crafts = craftResolver();
                if (actionContext.Parameters.TryGetValue("educationOption", out var selected))
                {
                    if (selected.Equals("standard", StringComparison.OrdinalIgnoreCase))
                        return education.GetEducationLevel(target) < 5;

                    if (selected.StartsWith("craft:", StringComparison.OrdinalIgnoreCase)
                        && crafts is not null)
                    {
                        var craftId = selected["craft:".Length..];
                        return crafts.GetEducationOptions(target)
                            .Any(option => option.CraftId.Equals(craftId, StringComparison.OrdinalIgnoreCase));
                    }

                    return false;
                }

                return education.GetEducationLevel(target) < 5
                    || crafts?.GetEducationOptions(target).Count > 0;
            },

            Execute = actionContext =>
            {
                var actor = actionContext.Actor;
                var target = actionContext.Target;
                if (target.Age < 18)
                {
                    return new GameActionResult(
                        false,
                        "Children use Help in Education instead of paid education.");
                }

                if (!economy.CanAfford(actor, EducationCost))
                {
                    return new GameActionResult(false, "Education is no longer available.");
                }

                var selected = actionContext.Parameters.TryGetValue("educationOption", out var option)
                    ? option
                    : "standard";

                if (selected.StartsWith("craft:", StringComparison.OrdinalIgnoreCase))
                {
                    var crafts = craftResolver();
                    var craftId = selected["craft:".Length..];
                    if (crafts is null
                        || !crafts.GetEducationOptions(target)
                            .Any(item => item.CraftId.Equals(craftId, StringComparison.OrdinalIgnoreCase)))
                    {
                        return new GameActionResult(false, "Craft education is no longer available.");
                    }

                    economy.ChangeWealth(actor, -EducationCost);
                    var result = crafts.StudyCraft(target, craftId);
                    return new GameActionResult(
                        result.Attempted,
                        result.Message,
                        result.Attempted
                            ? ActionReasonCodes.Executed
                            : ActionReasonCodes.NoLongerEligible);
                }

                if (!selected.Equals("standard", StringComparison.OrdinalIgnoreCase)
                    || education.GetEducationLevel(target) >= 5)
                {
                    return new GameActionResult(false, "Standard education is no longer available.");
                }

                economy.ChangeWealth(actor, -EducationCost);

                var intellect = stats.GetStats(target)
                    .First(stat => stat.Id.Equals("intellect", StringComparison.OrdinalIgnoreCase))
                    .Value;

                var successChance =
                    PersonalityInfluence.AdjustProbability(
                        EducationProgressionRules.GetPaidEducationSuccessChance(intellect),
                        target,
                        melancholic: 0.10,
                        choleric: -0.10);

                var success = random.NextDouble() < successChance;
                if (success)
                {
                    education.IncreaseEducation(target);
                    events.Publish(new GameEvent
                    {
                        Type = "education.success",
                        Year = actionContext.GameState.Year,
                        SubjectId = target.Id,
                        RelatedPersonIds = actor.Id == target.Id ? [] : [actor.Id],
                        Data = new Dictionary<string, string>
                        {
                            ["level"] = education.GetEducationLevel(target).ToString(),
                            ["text"] =
                                $"{family.GetDisplayName(target)} successfully completed a course, " +
                                $"reaching education level {education.GetEducationLevel(target)}."
                        }
                    });
                }
                else
                {
                    events.Publish(new GameEvent
                    {
                        Type = "education.failure",
                        Year = actionContext.GameState.Year,
                        SubjectId = target.Id,
                        RelatedPersonIds = actor.Id == target.Id ? [] : [actor.Id],
                        Data = new Dictionary<string, string>
                        {
                            ["text"] =
                                $"{family.GetDisplayName(target)} attempted further education, but did not advance this year."
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
        IEconomyService economy,
        IStatsService stats,
        IGameRandom random,
        IGameEventBus events,
        EducationEraCatalog eras)
    {
        return new GameActionDefinition
        {
            Id =
                "education.help_learning",

            Label =
                "Help in Education",

            Description =
                "Spend the year helping the selected young relative in this household study. " +
                "Available from age 6 through 17 and costs no money. Natural childhood Education " +
                "broadly follows the child's Intellect; household help can raise a child " +
                "one level beyond that natural ceiling. Success depends on both the child's " +
                "Intellect and the helper's Intellect.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.QueuedActionsEarly,

            IsAvailable =
                actionContext =>
                {
                    var helper =
                        actionContext.Actor;

                    var child =
                        actionContext.Target;

                    if (!helper.Tags.Has(
                            "state.alive")
                        || !actionContext.ActorHasControl
                        || !child.Tags.Has(
                            "state.alive")
                        || child.Id == helper.Id
                        || child.Age < HelpLearningMinimumAge
                        || child.Age >= HelpLearningAdultAge)
                    {
                        return false;
                    }

                    var childIntellect =
                        stats.GetStats(child)
                            .First(stat =>
                                stat.Id.Equals(
                                    "intellect",
                                    StringComparison.OrdinalIgnoreCase))
                            .Value;

                    var era =
                        eras.GetRule(
                            actionContext.GameState.Year);

                    if (education.GetEducationLevel(child)
                        >= EducationProgressionRules.GetHelpedChildhoodCeiling(
                            childIntellect,
                            era.HelpedMaxLevel))
                    {
                        return false;
                    }

                    return HouseholdKinshipRules.IsSupportedResidentRelative(
                        helper,
                        child,
                        family,
                        economy);
                },

            Execute =
                actionContext =>
                {
                    var helper =
                        actionContext.Actor;

                    var child =
                        actionContext.Target;

                    if (!helper.Tags.Has(
                            "state.alive")
                        || !child.Tags.Has(
                            "state.alive")
                        || child.Age < HelpLearningMinimumAge
                        || child.Age >= HelpLearningAdultAge
                        || !HouseholdKinshipRules.IsSupportedResidentRelative(
                            helper,
                            child,
                            family,
                            economy))
                    {
                        return new GameActionResult(
                            false,
                            "Help in Learning is no longer available.");
                    }

                    var childIntellect =
                        stats.GetStats(child)
                            .First(stat =>
                                stat.Id.Equals(
                                    "intellect",
                                    StringComparison.OrdinalIgnoreCase))
                            .Value;

                    var era =
                        eras.GetRule(
                            actionContext.GameState.Year);

                    if (education.GetEducationLevel(child)
                        >= EducationProgressionRules.GetHelpedChildhoodCeiling(
                            childIntellect,
                            era.HelpedMaxLevel))
                    {
                        return new GameActionResult(
                            false,
                            "The child has reached the Education level that household help can currently support.");
                    }

                    var helperIntellect =
                        stats.GetStats(helper)
                            .First(stat =>
                                stat.Id.Equals(
                                    "intellect",
                                    StringComparison.OrdinalIgnoreCase))
                            .Value;

                    var successChance =
                        EducationProgressionRules
                            .GetHelpInEducationSuccessChance(
                                childIntellect,
                                helperIntellect);

                    successChance =
                        PersonalityInfluence.AdjustProbability(
                            successChance,
                            child,
                            melancholic: 0.10,
                            choleric: -0.10);

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
                                    [helper.Id],

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
                                            $"{family.GetDisplayName(helper)} " +
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
                                    [helper.Id],

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["chance"] =
                                            successChance
                                                .ToString(
                                                    "0.00"),

                                        ["text"] =
                                            $"{family.GetDisplayName(helper)} " +
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
