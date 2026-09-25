using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Personality;

public sealed class PersonalityPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        EventPresentationRegistration.Register(context);
        var gameState = Require<IGameState>(context, "Game state");
        var family = Require<IFamilyService>(context, "Family service");
        var events = Require<IGameEventBus>(context, "Game event bus");
        var systems = Require<IYearSystemRegistry>(context, "Year system registry");
        var actions = Require<IActionRegistry>(context, "Action registry");
        var random = Require<IGameRandom>(context, "Game random service");
        var economy = Require<IEconomyService>(context, "Economy service");
        var data = Require<IGameDataService>(context, "Game data service");
        var locations = Require<ILocationService>(context, "Location service");
        var institutions = Require<ITownInstitutionService>(context, "Town institution service");
        var studyRules = ReligiousStudyChurchRules.Load(data);

        var personality = new StandardPersonalityService(
            gameState,
            family);

        context.AddService<IPersonalityService>(personality);

        var reconciliation = context.GetService<IStateReconciliationLifecycle>()
            ?? throw new InvalidOperationException(
                "State reconciliation lifecycle is unavailable.");

        reconciliation.Register(
            "personality.components_and_tags",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad,
                ReconciliationLifecycleStage.BeforeYear,
                ReconciliationLifecycleStage.AfterYear,
                ReconciliationLifecycleStage.AfterImmediateAction,
                ReconciliationLifecycleStage.AfterQueuedAction,
                ReconciliationLifecycleStage.AfterPersonCreated
            ],
            _ => personality.ReconcileAll(),
            order: 25);

        events.EventPublished +=
            (_, gameEvent) =>
            {
                if (gameEvent.Type.Equals(
                        "game.started",
                        StringComparison.OrdinalIgnoreCase))
                {
                    // New-game profile creation is an explicit creation path,
                    // not a presentation-time repair. Career initialization
                    // later in the same event needs temperament tags.
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

                foreach (var id in gameEvent.RelatedPersonIds)
                {
                    var person = gameState.People.FirstOrDefault(
                        candidate => candidate.Id == id);

                    if (person is not null)
                        personality.ReconcilePerson(person);
                }
            };

        systems.Register(
            new PersonalityAssignmentYearSystem(personality));

        systems.Register(
            new PersonalityPostYearSystem(personality));

        systems.Register(
            new MoralsDeteriorationYearSystem(
                personality,
                economy,
                random,
                events));

        actions.Register(
            CreateReligiousStudyAction(
                studyRules,
                family,
                personality,
                economy,
                random,
                events,
                locations,
                institutions));

        context.Log(
            "Personality mechanics registered; Religious Study is provided through the local Church.");
    }

    private static GameActionDefinition CreateReligiousStudyAction(
        ReligiousStudyChurchRules rules,
        IFamilyService family,
        IPersonalityService personality,
        IEconomyService economy,
        IGameRandom random,
        IGameEventBus events,
        ILocationService locations,
        ITownInstitutionService institutions) =>
        new()
        {
            Id = "personality.religious_study",
            Presentation = new()
            {
                Emoji = "📖",
                Categories = [ActionPresentationCategories.Personal],
                AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth,
                GroupOrder = 100,
                ShowInPrimaryActionList = false
            },
            Label = "Religious Study",
            Description =
                "Spend the year in deliberate religious or moral reflection for 3,000 zł. Success depends on the local Church tier (45–65%); a Good person instead gains protection against the next downward Morals shift.",
            DisplayCost = rules.BaseCost,
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.MoralsReflection,

            EvaluateAvailability = actionContext =>
            {
                var actor = actionContext.Actor;
                if (actor.Id != actionContext.Target.Id
                    || !actor.Tags.Has("state.alive")
                    || !actionContext.ActorHasControl
                    || actor.Age < 18
                    || actor.Tags.Has("state.imprisoned"))
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "Religious Study is available only to the active living adult controller.");
                }

                var churchTier = ResolveChurchTier(
                    actor,
                    actionContext.ScheduledExecutionYear,
                    locations,
                    institutions);
                var chance = rules.GetSuccessChance(churchTier);
                var metadata = new Dictionary<string, string>
                {
                    ["churchTier"] = churchTier.ToString(CultureInfo.InvariantCulture),
                    ["successChance"] = chance.ToString(CultureInfo.InvariantCulture),
                    ["cost"] = rules.BaseCost.ToString(CultureInfo.InvariantCulture)
                };

                // S9 makes the local Church an underlying mechanical
                // requirement, not merely a presentation route.
                if (churchTier <= 0)
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.ResourceUnavailable,
                        "Religious Study requires a local Church.",
                        presentationMetadata: metadata);
                }

                return economy.CanAfford(actor, rules.BaseCost)
                    ? ActionEvaluationResult.Allowed(
                        presentationMetadata: metadata)
                    : ActionEvaluationResult.Denied(
                        ActionReasonCodes.InsufficientFunds,
                        $"The household cannot afford {rules.BaseCost:N0} zł for Religious Study.",
                        presentationMetadata: metadata);
            },

            Execute = actionContext =>
            {
                var actor = actionContext.Actor;
                var churchTier = ResolveChurchTier(
                    actor,
                    actionContext.GameState.Year,
                    locations,
                    institutions);
                var successChance = rules.GetSuccessChance(churchTier);

                if (churchTier <= 0
                    || !economy.CanAfford(actor, rules.BaseCost))
                {
                    return new GameActionResult(
                        false,
                        "Religious Study is no longer available or affordable.");
                }

                economy.ChangeWealth(actor, -rules.BaseCost);
                var possessive = family.GetSex(actor) == Sex.Female
                    ? "her"
                    : "his";

                if (random.NextDouble() >= successChance)
                {
                    events.Publish(
                        new GameEvent
                        {
                            Type = "personality.religious_study",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["success"] = "false",
                                ["churchTier"] = churchTier.ToString(CultureInfo.InvariantCulture),
                                ["successChance"] = successChance.ToString(CultureInfo.InvariantCulture),
                                ["text"] = $"{family.GetDisplayName(actor)} devoted time to religious study, but {possessive} outlook did not change."
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
                events.Publish(
                    new GameEvent
                    {
                        Type = "personality.religious_study",
                        Year = actionContext.GameState.Year,
                        SubjectId = actor.Id,
                        Data = new Dictionary<string, string>
                        {
                            ["success"] = "true",
                            ["churchTier"] = churchTier.ToString(CultureInfo.InvariantCulture),
                            ["successChance"] = successChance.ToString(CultureInfo.InvariantCulture),
                            ["from"] = before ?? string.Empty,
                            ["to"] = after ?? string.Empty,
                            ["text"] = before == "Good"
                                ? $"{family.GetDisplayName(actor)} deepened {possessive} religious convictions."
                                : $"{family.GetDisplayName(actor)} emerged from religious study with a more benevolent outlook."
                        }
                    });
                return new GameActionResult(true);
            }
        };

    private static int ResolveChurchTier(
        IPerson person,
        int year,
        ILocationService locations,
        ITownInstitutionService institutions)
    {
        var town = locations.GetLocation(person).HomeTown;
        return institutions.Resolve(town, year).GetTier("church");
    }

    private static T Require<T>(
        IGamePluginContext context,
        string label)
        where T : class =>
        context.GetService<T>()
        ?? throw new InvalidOperationException($"{label} is unavailable.");
}
