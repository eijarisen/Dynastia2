using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed partial class JusticePlugin
{
    private static void RegisterCriminalOccupationActions(
        IActionRegistry actions,
        CriminalOccupationService crime,
        IFamilyService family,
        IEconomyService economy,
        IPersonalityService personality,
        IGameRandom random,
        IGameEventBus events,
        CriminalOccupationRules rules)
    {
        actions.Register(new GameActionDefinition
        {
            Id = rules.StartActionId,
            Presentation = new()
            {
                Emoji = "⚙️",
                Categories = [ActionPresentationCategories.Career],
                AdjacencyGroup = ActionPresentationGroups.CareerWork,
                GroupOrder = 60
            },
            Label = "Commit a Crime",
            Description =
                "Begin a persistent Life of Crime and attempt the first Heist this year. " +
                "Only Evil adults can choose this path, and it can only be started once.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            EvaluateAvailability = context =>
            {
                if (!CanDirectOwnOccupation(context)
                    || !context.Target.Tags.Has("state.alive")
                    || context.Target.Age < rules.MinimumAge
                    || context.Target.Tags.Has("state.imprisoned")
                    || crime.HasStartedLifeOfCrime(context.Target))
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "Life of Crime can only be started once by the controlled adult themself.");
                }

                var morals = personality.GetPersonality(context.Target)?.Morals;
                if (!string.Equals(morals, rules.RequiredMorals, StringComparison.OrdinalIgnoreCase))
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "Only a person with Evil morals can deliberately begin a Life of Crime.");
                }

                return ActionEvaluationResult.Allowed(economy.GetHouseholdId(context.Target));
            },
            Execute = context =>
            {
                if (!CanDirectOwnOccupation(context)
                    || !context.Target.Tags.Has("state.alive")
                    || context.Target.Age < rules.MinimumAge
                    || context.Target.Tags.Has("state.imprisoned")
                    || crime.HasStartedLifeOfCrime(context.Target))
                {
                    return new GameActionResult(false, "Life of Crime is no longer available.", ActionReasonCodes.NoLongerEligible);
                }

                var morals = personality.GetPersonality(context.Target)?.Morals;
                if (!string.Equals(morals, rules.RequiredMorals, StringComparison.OrdinalIgnoreCase))
                    return new GameActionResult(false, "Only a person with Evil morals can deliberately begin a Life of Crime.", ActionReasonCodes.NoLongerEligible);

                if (!crime.StartLifeOfCrime(context.Target))
                    return new GameActionResult(false, "Life of Crime could not be started.");

                return crime.PerformFirstHeist(context.Target)
                    ? new GameActionResult(true)
                    : new GameActionResult(false, "The first Heist could not be attempted.");
            }
        });

        actions.Register(new GameActionDefinition
        {
            Id = rules.StopActionId,
            Presentation = new()
            {
                Emoji = "⚙️",
                Categories = [ActionPresentationCategories.Career],
                AdjacencyGroup = ActionPresentationGroups.CareerWork,
                GroupOrder = 70
            },
            Label = "Leave Life of Crime",
            Description =
                "End the criminal occupation. Mastery is preserved, but Life of Crime cannot be started again later.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            EvaluateAvailability = context =>
                CanDirectOwnOccupation(context)
                && context.Target.Tags.Has("state.alive")
                && !context.Target.Tags.Has("state.imprisoned")
                && crime.IsActive(context.Target)
                    ? ActionEvaluationResult.Allowed(economy.GetHouseholdId(context.Target))
                    : ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "The selected person is not currently living a Life of Crime."),
            Execute = context =>
                CanDirectOwnOccupation(context)
                && context.Target.Tags.Has("state.alive")
                && !context.Target.Tags.Has("state.imprisoned")
                && crime.IsActive(context.Target)
                    ? new GameActionResult(
                        crime.EndLifeOfCrime(context.Target, "left voluntarily"))
                    : new GameActionResult(
                        false,
                        "Life of Crime is no longer active for the controlled person.",
                        ActionReasonCodes.NoLongerEligible)
        });

        actions.Register(new GameActionDefinition
        {
            Id = "justice.ask_to_quit_crime",
            Presentation = new()
            {
                Emoji = "🛑",
                Categories = [ActionPresentationCategories.Career, ActionPresentationCategories.Family],
                AdjacencyGroup = ActionPresentationGroups.CareerWork,
                GroupOrder = 80
            },
            Label = "Ask to Quit Crime",
            Description =
                "Ask your spouse to leave their Life of Crime. They may refuse; the success chance is the same as Ask to Quit Job.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            EvaluateAvailability = context =>
                CanAskSpouseToQuitCrime(context, family, economy)
                && crime.IsActive(context.Target)
                    ? ActionEvaluationResult.Allowed(economy.GetHouseholdId(context.Actor))
                    : ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "Only a spouse who is currently living a Life of Crime can be asked to quit."),
            Execute = context =>
            {
                if (!CanAskSpouseToQuitCrime(context, family, economy)
                    || !crime.IsActive(context.Target))
                {
                    return new GameActionResult(false);
                }

                if (random.NextDouble() > 0.5)
                {
                    return new GameActionResult(
                        crime.EndLifeOfCrime(
                            context.Target,
                            "quit at spouse's request"));
                }

                events.Publish(new GameEvent
                {
                    Type = "justice.ask_quit_crime_failure",
                    Year = context.GameState.Year,
                    SubjectId = context.Actor.Id,
                    RelatedPersonIds = [context.Target.Id],
                    Data = new Dictionary<string, string>
                    {
                        ["text"] =
                            $"{family.GetDisplayName(context.Actor)} asked " +
                            $"{family.GetDisplayName(context.Target)} to leave their Life of Crime, but they refused."
                    }
                });

                return new GameActionResult(true);
            }
        });
    }

    private static bool CanDirectOwnOccupation(GameActionContext context) =>
        context.ActorHasControl
        && context.Actor.Tags.Has("state.alive")
        && context.Actor.Id == context.Target.Id;

    private static bool CanAskSpouseToQuitCrime(
        GameActionContext context,
        IFamilyService family,
        IEconomyService economy)
    {
        if (!context.ActorHasControl
            || !context.Actor.Tags.Has("state.alive")
            || !context.Target.Tags.Has("state.alive")
            || context.Actor.Id == context.Target.Id
            || family.GetSpouse(context.Actor)?.Id != context.Target.Id)
        {
            return false;
        }

        var actorHouseholdId = economy.GetHouseholdId(context.Actor);
        return actorHouseholdId.HasValue
            && economy.GetHouseholdId(context.Target) == actorHouseholdId;
    }
}
