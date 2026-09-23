using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed partial class JusticePlugin
{
    private static void RegisterCourtActions(
        IActionRegistry actions,
        StandardJusticeService justice,
        ICriminalOccupationService criminalOccupation,
        IFamilyService family,
        IEconomyService economy,
        IStatsService stats,
        CourtJusticeRules rules)
    {
        actions.Register(new GameActionDefinition
        {
            Id = rules.Bail.ActionId,
            Presentation = new()
            {
                Emoji = "💵",
                Categories = [ActionPresentationCategories.Personal]
            },
            Label = "Bail Out",
            Description = "Pay the household's expensive bail cost to release the selected imprisoned household member. The criminal record remains.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            BypassGuards = true,
            EvaluateAvailability = context =>
                EvaluateBailAvailability(context, justice, economy),
            Execute = context =>
            {
                var evaluation = EvaluateBailAvailability(context, justice, economy);
                if (!evaluation.Available)
                    return new GameActionResult(false, evaluation.Reason, evaluation.ReasonCode);

                var cost = justice.GetBailCost(context.Target);
                economy.ChangeWealth(context.Actor, -cost);
                if (!justice.ReleaseFromPrison(context.Target))
                {
                    economy.ChangeWealth(context.Actor, cost);
                    return new GameActionResult(
                        false,
                        "The selected person is no longer imprisoned.",
                        ActionReasonCodes.NoLongerEligible);
                }

                context.EventBus.Publish(new GameEvent
                {
                    Type = "justice.bailed_out",
                    Year = context.GameState.Year,
                    SubjectId = context.Target.Id,
                    RelatedPersonIds = context.Actor.Id == context.Target.Id
                        ? []
                        : [context.Actor.Id],
                    Data = new Dictionary<string, string>
                    {
                        ["amount"] = cost.ToString(CultureInfo.InvariantCulture),
                        ["text"] = $"The household paid {cost:N0} zł to secure {family.GetDisplayName(context.Target)}'s release from prison."
                    }
                });

                return new GameActionResult(true);
            }
        });

        actions.Register(new GameActionDefinition
        {
            Id = rules.Escape.ActionId,
            Presentation = new()
            {
                Emoji = "🔓",
                Categories = [ActionPresentationCategories.Personal]
            },
            Label = "Attempt Escape",
            Description = "An imprisoned person with Intellect 5 may attempt one escape per imprisonment. Failure adds 3 years to the sentence.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            BypassGuards = true,
            EvaluateAvailability = context =>
                EvaluateEscapeAvailability(context, justice, economy, stats, rules),
            Execute = context =>
            {
                var evaluation = EvaluateEscapeAvailability(
                    context,
                    justice,
                    economy,
                    stats,
                    rules);
                if (!evaluation.Available)
                    return new GameActionResult(false, evaluation.Reason, evaluation.ReasonCode);

                justice.MarkEscapeAttempted(context.Target);
                var crime = criminalOccupation.GetSnapshot(context.Target);
                var isMastermind = crime.HasStartedLifeOfCrime
                    && crime.ArchetypeId.Equals("mastermind", StringComparison.OrdinalIgnoreCase);
                var chance = isMastermind
                    ? rules.Escape.MastermindSuccessChance
                    : rules.Escape.BaseSuccessChance;

                if (context.Random.NextDouble() < chance)
                {
                    justice.ReleaseFromPrison(context.Target);
                    context.EventBus.Publish(new GameEvent
                    {
                        Type = "justice.escape_success",
                        Year = context.GameState.Year,
                        SubjectId = context.Target.Id,
                        Data = new Dictionary<string, string>
                        {
                            ["chance"] = chance.ToString(CultureInfo.InvariantCulture),
                            ["text"] = $"{family.GetDisplayName(context.Target)} escaped from prison."
                        }
                    });
                    return new GameActionResult(true);
                }

                var remaining = justice.ExtendSentence(
                    context.Target,
                    rules.Escape.FailureSentenceExtensionYears);
                context.EventBus.Publish(new GameEvent
                {
                    Type = "justice.escape_failed",
                    Year = context.GameState.Year,
                    SubjectId = context.Target.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["years"] = rules.Escape.FailureSentenceExtensionYears.ToString(CultureInfo.InvariantCulture),
                        ["remainingSentence"] = remaining.ToString(CultureInfo.InvariantCulture),
                        ["chance"] = chance.ToString(CultureInfo.InvariantCulture),
                        ["text"] = $"{family.GetDisplayName(context.Target)}'s escape attempt failed, adding {rules.Escape.FailureSentenceExtensionYears} years to the sentence."
                    }
                });
                return new GameActionResult(true);
            }
        });
    }

    private static ActionEvaluationResult EvaluateBailAvailability(
        GameActionContext context,
        IJusticeService justice,
        IEconomyService economy)
    {
        if (!context.ActorHasControl
            || !context.Target.Tags.Has("state.alive")
            || !justice.IsImprisoned(context.Target)
            || !SharesHousehold(context.Actor, context.Target, economy))
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                "Bail is available only for an imprisoned member of the active household.");
        }

        var cost = justice.GetBailCost(context.Target);
        var wealth = economy.GetHousehold(context.Actor)?.Wealth ?? 0m;
        if (wealth < cost)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.InsufficientFunds,
                $"Bail costs {cost:N0} zł, but the household has only {wealth:N0} zł.");
        }

        return ActionEvaluationResult.Allowed(economy.GetHouseholdId(context.Actor));
    }

    private static ActionEvaluationResult EvaluateEscapeAvailability(
        GameActionContext context,
        IJusticeService justice,
        IEconomyService economy,
        IStatsService stats,
        CourtJusticeRules rules)
    {
        if (!context.ActorHasControl
            || !context.Target.Tags.Has("state.alive")
            || !justice.IsImprisoned(context.Target)
            || !SharesHousehold(context.Actor, context.Target, economy))
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                "Escape can only be attempted by an imprisoned member of the active household.");
        }

        var intellect = stats.GetStats(context.Target)
            .FirstOrDefault(stat => stat.Id.Equals("intellect", StringComparison.OrdinalIgnoreCase))?.Value
            ?? 0;
        if (intellect != rules.Escape.RequiresIntellect)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                $"Attempt Escape requires Intellect {rules.Escape.RequiresIntellect}.");
        }

        if (justice.HasAttemptedEscapeThisImprisonment(context.Target))
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                "Only one escape attempt is allowed during the current imprisonment.");
        }

        return ActionEvaluationResult.Allowed(economy.GetHouseholdId(context.Actor));
    }

    private static bool SharesHousehold(
        IPerson actor,
        IPerson target,
        IEconomyService economy)
    {
        var householdId = economy.GetHouseholdId(actor);
        return householdId.HasValue
            && economy.GetHouseholdId(target) == householdId;
    }
}
