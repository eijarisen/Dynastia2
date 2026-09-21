using Dynastia.Contracts;

namespace Dynastia.Mechanics.Education;

public sealed class EducationPlugin : IGamePlugin
{
    private const decimal StandardEducationCost = 5000m;
    private const decimal CraftEducationCost = 5000m;
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

        var locations = context.GetService<ILocationService>()
            ?? throw new InvalidOperationException("Location service is unavailable.");

        var institutions = context.GetService<ITownInstitutionService>()
            ?? throw new InvalidOperationException("Town institution service is unavailable.");

        var random = context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException("Game random service is unavailable.");

        var events = context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException("Game event bus is unavailable.");

        var systems = context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException("Year system registry is unavailable.");

        var actions = context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException("Action registry is unavailable.");

        var eras = EducationEraCatalog.Load(data);
        var localityRules = EducationLocalityRules.Load(data);

        var education = new StandardEducationService(
            family,
            stats,
            eras,
            locations,
            institutions,
            localityRules,
            gameState,
            () => context.GetService<ICommunityPolicyService>());

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
            eras,
            locations);

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
        EducationEraCatalog eras,
        ILocationService locations)
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
                        var town = locations.GetLocation(spouse).HomeTown;
                        var range = education.GetGeneratedAdultRange(
                            gameEvent.Year,
                            town);

                        education.SetEducationLevel(
                            spouse,
                            random.NextInt(
                                range.MinimumLevel,
                                range.MaximumLevel));
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
                        var town = locations.GetLocation(nanny).HomeTown;
                        var range = education.GetGeneratedAdultRange(
                            gameEvent.Year,
                            town);

                        education.SetEducationLevel(
                            nanny,
                            random.NextInt(
                                range.MinimumLevel,
                                range.MaximumLevel));
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
                "Choose standard education or study a Craft for 5,000 zł. The cost is charged when the attempt is made.",
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

                var crafts = craftResolver();
                if (actionContext.Parameters.TryGetValue("educationOption", out var selected))
                {
                    if (selected.Equals("standard", StringComparison.OrdinalIgnoreCase))
                    {
                        var selectedLocalCeiling = education.GetLocalEducationCeiling(
                            target,
                            actionContext.GameState.Year);
                        return education.GetEducationLevel(target) < selectedLocalCeiling
                            && economy.CanAfford(actor, StandardEducationCost);
                    }

                    if (selected.StartsWith("craft:", StringComparison.OrdinalIgnoreCase)
                        && crafts is not null)
                    {
                        var craftId = selected["craft:".Length..];
                        return economy.CanAfford(actor, CraftEducationCost)
                            && crafts.GetEducationOptions(target)
                                .Any(option => option.CraftId.Equals(craftId, StringComparison.OrdinalIgnoreCase));
                    }

                    return false;
                }

                var localCeiling = education.GetLocalEducationCeiling(
                    target,
                    actionContext.GameState.Year);
                var canStudyStandard =
                    education.GetEducationLevel(target) < localCeiling
                    && economy.CanAfford(actor, StandardEducationCost);

                var canStudyCraft =
                    economy.CanAfford(actor, CraftEducationCost)
                    && crafts?.GetEducationOptions(target).Count > 0;

                return canStudyStandard || canStudyCraft;
            },

            Execute = actionContext =>
            {
                var actor = actionContext.Actor;
                var target = actionContext.Target;
                if (target.Age < 18)
                {
                    return new GameActionResult(
                        false,
                        "Children use Help in Learning instead of paid education.");
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

                    if (!economy.CanAfford(actor, CraftEducationCost))
                    {
                        return new GameActionResult(false, "Craft education is no longer affordable.");
                    }

                    economy.ChangeWealth(actor, -CraftEducationCost);
                    var result = crafts.StudyCraft(target, craftId);
                    return new GameActionResult(
                        result.Attempted,
                        result.Message,
                        result.Attempted
                            ? ActionReasonCodes.Executed
                            : ActionReasonCodes.NoLongerEligible);
                }

                var localCeiling = education.GetLocalEducationCeiling(
                    target,
                    actionContext.GameState.Year);
                if (!selected.Equals("standard", StringComparison.OrdinalIgnoreCase)
                    || education.GetEducationLevel(target) >= localCeiling)
                {
                    return new GameActionResult(
                        false,
                        localCeiling <= 0
                            ? "No ordinary local schooling is available."
                            : $"The local School can only support Education up to level {localCeiling}.");
                }

                if (!economy.CanAfford(actor, StandardEducationCost))
                {
                    return new GameActionResult(false, "Standard education is no longer affordable.");
                }

                economy.ChangeWealth(actor, -StandardEducationCost);

                var successChance = Math.Clamp(
                    education.GetPaidEducationSuccessChance(target)
                    * HouseholdLifestyleRules.GetEducationChanceMultiplier(target),
                    0,
                    1);

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
                "Help in Learning",

            Description =
                "Spend the year helping your selected child study. " +
                "Available from age 6 through 17 and costs no money. Natural childhood Education " +
                "broadly follows the child's Intellect; parental help can raise a child " +
                "beyond the local School ceiling when the parent is sufficiently educated. " +
                "The child can never advance beyond the parent's own Education, and success depends on both Intellect scores.",

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

                    var helperEducation = education.GetEducationLevel(helper);
                    var helpedCeiling =
                        EducationProgressionRules.GetHelpedChildhoodCeiling(
                            childIntellect,
                            era.HelpedMaxLevel,
                            helperEducation);

                    if (education.GetEducationLevel(child) >= helpedCeiling)
                        return false;

                    return IsParentOf(helper, child, family)
                        && economy.GetHouseholdId(helper) is Guid helperHouseholdId
                        && economy.GetHouseholdId(child) == helperHouseholdId;
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
                        || !IsParentOf(helper, child, family)
                        || economy.GetHouseholdId(helper) is not Guid helperHouseholdId
                        || economy.GetHouseholdId(child) != helperHouseholdId)
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

                    var helperEducation = education.GetEducationLevel(helper);
                    var helpedCeiling =
                        EducationProgressionRules.GetHelpedChildhoodCeiling(
                            childIntellect,
                            era.HelpedMaxLevel,
                            helperEducation);

                    if (education.GetEducationLevel(child) >= helpedCeiling)
                    {
                        return new GameActionResult(
                            false,
                            "The child has reached the Education level that this parent can personally teach.");
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

                    successChance = Math.Clamp(
                        successChance
                        * HouseholdLifestyleRules.GetEducationChanceMultiplier(child),
                        0,
                        1);

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

    private static bool IsParentOf(
        IPerson parent,
        IPerson child,
        IFamilyService family) =>
        family.GetFather(child)?.Id == parent.Id
        || family.GetMother(child)?.Id == parent.Id;

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
