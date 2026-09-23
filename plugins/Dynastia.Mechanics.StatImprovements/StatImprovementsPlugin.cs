using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.StatImprovements;

public sealed class StatImprovementsPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var data = Require<IGameDataService>(context, "Game data service");
        var gameState = Require<IGameState>(context, "Game state");
        var stats = Require<IStatsService>(context, "Stats service");
        var family = Require<IFamilyService>(context, "Family service");
        var economy = Require<IEconomyService>(context, "Economy service");
        var households = Require<IHouseholdService>(context, "Household service");
        var health = Require<IHealthService>(context, "Health service");
        var historical = Require<IHistoricalActionVariantService>(context, "Historical action variant service");
        var locations = Require<ILocationService>(context, "Location service");
        var facilities = Require<ITownFacilityQualityService>(context, "Town facility quality service");
        var actions = Require<IActionRegistry>(context, "Action registry");

        var definitions = StatImprovementRules.Load(data);

        // S9 keeps the stable action IDs but makes their presentation and cost
        // local-facility dependent. A dynamic provider guarantees that every UI
        // and direct evaluator sees the current town's Medical tier and price.
        actions.RegisterDynamicProvider(
            (_, target) =>
                definitions.Select(definition =>
                    CreateAction(
                        definition,
                        target,
                        gameState,
                        stats,
                        family,
                        economy,
                        households,
                        health,
                        historical,
                        locations,
                        facilities)));

        context.Log(
            "Paid stat-improvement actions registered through local Medical facilities.");
    }

    private static ActionPresentationMetadata GetPresentation(string statId)
    {
        var (emoji, order) = statId.ToLowerInvariant() switch
        {
            "strength" => ("🏋️", 40),
            "intellect" => ("🧠", 50),
            "immunity" => ("🛡️", 60),
            "appeal" => ("✨", 70),
            "longevity" => ("🩺", 80),
            "fertility" => ("🧬", 90),
            _ => ("⚙️", 0)
        };
        return new ActionPresentationMetadata
        {
            Emoji = emoji,
            Categories = [ActionPresentationCategories.Personal],
            AdjacencyGroup = order > 0 ? ActionPresentationGroups.TreatmentGrowth : null,
            GroupOrder = order
        };
    }

    private static GameActionDefinition CreateAction(
        PaidStatImprovementDefinition definition,
        IPerson presentationTarget,
        IGameState gameState,
        IStatsService stats,
        IFamilyService family,
        IEconomyService economy,
        IHouseholdService households,
        IHealthService health,
        IHistoricalActionVariantService historical,
        ILocationService locations,
        ITownFacilityQualityService facilities)
    {
        var canonical = historical.GetCanonicalVariant(definition.ActionId)
            ?? throw new InvalidDataException(
                $"Missing historical action data for '{definition.ActionId}'.");
        var variant = historical.GetVariant(definition.ActionId, gameState.Year)
            ?? canonical;
        var medical = GetMedicalQuality(
            presentationTarget,
            gameState.Year,
            locations,
            facilities);
        var displayCost = StatImprovementRules.CalculateCost(
            definition.BaseCost,
            medical.TreatmentCostMultiplier);

        return new GameActionDefinition
        {
            Id = definition.ActionId,
            Presentation = GetPresentation(definition.StatId),
            Label = variant.Label,
            Description =
                $"{variant.Description} Requires a Tier {definition.MinimumMedicalTier}+ local medical facility.",
            DisplayCost = displayCost,
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,

            EvaluateAvailability = actionContext =>
                EvaluateAvailability(
                    actionContext,
                    definition,
                    stats,
                    economy,
                    households,
                    historical,
                    locations,
                    facilities),

            Execute = actionContext =>
            {
                var actor = actionContext.Actor;
                var target = actionContext.Target;

                var evaluation = EvaluateAvailability(
                    actionContext,
                    definition,
                    stats,
                    economy,
                    households,
                    historical,
                    locations,
                    facilities);
                if (!evaluation.Available)
                {
                    return new GameActionResult(
                        false,
                        evaluation.Reason,
                        evaluation.ReasonCode);
                }

                var medicalNow = GetMedicalQuality(
                    target,
                    actionContext.GameState.Year,
                    locations,
                    facilities);
                var currentCost = StatImprovementRules.CalculateCost(
                    definition.BaseCost,
                    medicalNow.TreatmentCostMultiplier);

                var before = GetEffectiveStat(
                    stats,
                    target,
                    definition.StatId);

                if (!stats.TryIncreaseAcquiredStat(
                        target,
                        definition.StatId))
                {
                    return new GameActionResult(
                        false,
                        $"{definition.StatName} is already at its maximum.");
                }

                economy.ChangeWealth(actor, -currentCost);

                var after = GetEffectiveStat(
                    stats,
                    target,
                    definition.StatId);

                if (definition.StatId.Equals(
                        "fertility",
                        StringComparison.OrdinalIgnoreCase)
                    && before == 0
                    && after >= 1)
                {
                    // No separate infertility condition exists in the current
                    // game, but retain the future-compatible cleanup behavior.
                    health.RemoveCondition(target, "infertility");
                    target.Tags.Remove("state.infertile");
                    target.Tags.Remove("trait.infertile");
                }

                var displayName = family.GetDisplayName(target);
                var currentVariant = historical.GetVariant(
                        definition.ActionId,
                        actionContext.GameState.Year)
                    ?? canonical;
                var narrative =
                    definition.StatId.Equals(
                            "fertility",
                            StringComparison.OrdinalIgnoreCase)
                        && before == 0
                        && after == 1
                            ? $"{displayName} {currentVariant.Narrative} and overcame infertility."
                            : $"{displayName} {currentVariant.Narrative} and improved their {definition.StatName}.";

                actionContext.EventBus.Publish(
                    new GameEvent
                    {
                        Type = "stats.paid_improvement",
                        Year = actionContext.GameState.Year,
                        SubjectId = target.Id,
                        RelatedPersonIds = actor.Id == target.Id
                            ? []
                            : [actor.Id],
                        Data = new Dictionary<string, string>
                        {
                            ["actionId"] = definition.ActionId,
                            ["statId"] = definition.StatId,
                            ["statName"] = definition.StatName,
                            ["medicalTier"] = medicalNow.Tier.ToString(CultureInfo.InvariantCulture),
                            ["cost"] = currentCost.ToString(CultureInfo.InvariantCulture),
                            ["previousValue"] = before.ToString(CultureInfo.InvariantCulture),
                            ["newValue"] = after.ToString(CultureInfo.InvariantCulture),
                            ["text"] = narrative
                        }
                    });

                return new GameActionResult(true);
            }
        };
    }

    private static ActionEvaluationResult EvaluateAvailability(
        GameActionContext actionContext,
        PaidStatImprovementDefinition definition,
        IStatsService stats,
        IEconomyService economy,
        IHouseholdService households,
        IHistoricalActionVariantService historical,
        ILocationService locations,
        ITownFacilityQualityService facilities)
    {
        var actor = actionContext.Actor;
        var target = actionContext.Target;

        if (!actor.Tags.Has("state.alive")
            || !actionContext.ActorHasControl
            || !target.Tags.Has("state.alive")
            || target.Age < 18)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                "This medical improvement is only available to a living adult in the controlled household.");
        }

        var historicallyAvailable = historical.GetVariant(
                definition.ActionId,
                actionContext.GameState.Year)
            is not null;
        if (!historicallyAvailable
            && !ActionCompatibilityParameters.IsRestoredQueuedAction(
                actionContext.Parameters))
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                "This improvement is not available in the current historical period.");
        }

        var actorHousehold = households.ResolveHouseholdHead(actor);
        var targetHousehold = households.ResolveHouseholdHead(target);
        if (actorHousehold?.Id != actor.Id
            || targetHousehold?.Id != actor.Id)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                "The selected adult must belong to the active household.");
        }

        if (GetEffectiveStat(stats, target, definition.StatId) >= 5)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                $"{definition.StatName} is already at its maximum.");
        }

        var medical = GetMedicalQuality(
            target,
            actionContext.GameState.Year,
            locations,
            facilities);
        var currentCost = StatImprovementRules.CalculateCost(
            definition.BaseCost,
            medical.TreatmentCostMultiplier);
        var metadata = new Dictionary<string, string>
        {
            ["medicalTier"] = medical.Tier.ToString(CultureInfo.InvariantCulture),
            ["minimumMedicalTier"] = definition.MinimumMedicalTier.ToString(CultureInfo.InvariantCulture),
            ["cost"] = currentCost.ToString(CultureInfo.InvariantCulture)
        };

        // S9 deliberately has no visiting-physician/remote fallback. The
        // selected person's own residence must meet the minimum Medical tier.
        if (!medical.IsAvailable
            || medical.Tier < definition.MinimumMedicalTier)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.ResourceUnavailable,
                $"{definition.StatName} improvement requires a local Medical facility of Tier {definition.MinimumMedicalTier} or higher.",
                presentationMetadata: metadata);
        }

        var finance = economy.GetHousehold(actor);
        if (finance is null
            || finance.Wealth < currentCost)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.InsufficientFunds,
                $"The household cannot afford {currentCost:N0} zł for this medical improvement.",
                presentationMetadata: metadata);
        }

        return ActionEvaluationResult.Allowed(
            presentationMetadata: metadata);
    }

    private static MedicalQualityInfo GetMedicalQuality(
        IPerson target,
        int year,
        ILocationService locations,
        ITownFacilityQualityService facilities)
    {
        var town = locations.GetLocation(target).HomeTown;
        return facilities.GetMedicalQuality(town, year);
    }

    private static int GetEffectiveStat(
        IStatsService stats,
        IPerson person,
        string statId) =>
        stats.GetStats(person)
            .First(stat => stat.Id.Equals(
                statId,
                StringComparison.OrdinalIgnoreCase))
            .Value;

    private static T Require<T>(
        IGamePluginContext context,
        string label)
        where T : class =>
        context.GetService<T>()
        ?? throw new InvalidOperationException($"{label} is unavailable.");
}
