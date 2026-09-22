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
        CriminalOccupationRules rules)
    {
        actions.Register(new GameActionDefinition
        {
            Id = rules.StartActionId,
            Label = "Commit a Crime",
            Description =
                "Begin a persistent Life of Crime and attempt the first Heist this year. " +
                "Only Evil adults can choose this path, and it can only be started once.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            EvaluateAvailability = context =>
            {
                if (!CanDirectOccupation(context, family, economy)
                    || !context.Target.Tags.Has("state.alive")
                    || context.Target.Age < rules.MinimumAge
                    || context.Target.Tags.Has("state.imprisoned")
                    || crime.HasStartedLifeOfCrime(context.Target))
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "Life of Crime can only be started once by an eligible adult in the household.");
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
            Label = "Leave Life of Crime",
            Description =
                "End the criminal occupation. Mastery is preserved, but Life of Crime cannot be started again later.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            EvaluateAvailability = context =>
                CanDirectOccupation(context, family, economy)
                && context.Target.Tags.Has("state.alive")
                && !context.Target.Tags.Has("state.imprisoned")
                && crime.IsActive(context.Target)
                    ? ActionEvaluationResult.Allowed(economy.GetHouseholdId(context.Target))
                    : ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "The selected person is not currently living a Life of Crime."),
            Execute = context =>
                new GameActionResult(
                    crime.EndLifeOfCrime(context.Target, "left voluntarily"))
        });
    }

    private static bool CanDirectOccupation(
        GameActionContext context,
        IFamilyService family,
        IEconomyService economy)
    {
        if (!context.ActorHasControl
            || !context.Actor.Tags.Has("state.alive"))
        {
            return false;
        }

        if (context.Actor.Id == context.Target.Id)
            return true;

        return HouseholdKinshipRules.IsSupportedResidentRelative(
            context.Actor,
            context.Target,
            family,
            economy,
            requireAdult: true);
    }
}
