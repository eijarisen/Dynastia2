using Dynastia.Contracts;

namespace Dynastia.Mechanics.Wellbeing;

public sealed partial class WellbeingPlugin
{
    private static void RegisterTherapy(
        IActionRegistry actions,
        IFamilyService family,
        IHealthService health,
        IEconomyService economy,
        IStatsService stats,
        IGameRandom random,
        IGameEventBus events,
        IGameState gameState,
        IHistoricalActionVariantService historical,
        ILocationService locations,
        ITownFacilityQualityService facilityQuality)
    {
        var canonical =
            historical.GetCanonicalVariant(
                "wellbeing.therapy")
            ?? throw new InvalidDataException(
                "Missing historical action data for 'wellbeing.therapy'.");

        actions.RegisterDynamicProvider(
            (_, target) =>
            {
                var variant =
                    historical.GetVariant(
                        "wellbeing.therapy",
                        gameState.Year)
                    ?? canonical;

                var medical = GetMedicalQuality(
                    target,
                    gameState.Year,
                    locations,
                    facilityQuality);

                var treatmentCost =
                    MedicalTreatmentRules.AdjustCost(
                        TherapyCost,
                        medical.TreatmentCostMultiplier);

                return
                [
                    new GameActionDefinition
                    {
                        Id = "wellbeing.therapy",
                        Label = variant.Label,
                        Description = variant.Description,
                        DisplayCost = treatmentCost,
                        Mode = ActionExecutionMode.Queued,
                        QueuePhase = YearPhase.QueuedActionsEarly,

                        IsAvailable = actionContext =>
                        {
                            var historicallyAvailable =
                                historical.GetVariant(
                                    "wellbeing.therapy",
                                    actionContext.GameState.Year)
                                is not null;

                            if (!historicallyAvailable
                                && !ActionCompatibilityParameters.IsRestoredQueuedAction(
                                    actionContext.Parameters))
                            {
                                return false;
                            }

                            if (!CanActorAct(actionContext)
                                || !actionContext.Target.Tags.Has("state.alive")
                                || !HasTherapyCondition(health, actionContext.Target))
                            {
                                return false;
                            }

                            var currentMedical = GetMedicalQuality(
                                actionContext.Target,
                                actionContext.GameState.Year,
                                locations,
                                facilityQuality);
                            if (!currentMedical.IsAvailable)
                                return false;

                            var currentCost =
                                MedicalTreatmentRules.AdjustCost(
                                    TherapyCost,
                                    currentMedical.TreatmentCostMultiplier);

                            return economy.CanAfford(
                                actionContext.Actor,
                                currentCost);
                        },

                        Execute = actionContext =>
                        {
                            var actor = actionContext.Actor;
                            var treatmentTarget = actionContext.Target;

                            var historicallyAvailable =
                                historical.GetVariant(
                                    "wellbeing.therapy",
                                    actionContext.GameState.Year)
                                is not null;

                            if (!historicallyAvailable
                                && !ActionCompatibilityParameters.IsRestoredQueuedAction(
                                    actionContext.Parameters))
                            {
                                return new GameActionResult(false);
                            }

                            var currentMedical = GetMedicalQuality(
                                treatmentTarget,
                                actionContext.GameState.Year,
                                locations,
                                facilityQuality);
                            var currentCost =
                                MedicalTreatmentRules.AdjustCost(
                                    TherapyCost,
                                    currentMedical.TreatmentCostMultiplier);

                            if (!currentMedical.IsAvailable
                                || !economy.CanAfford(actor, currentCost)
                                || !treatmentTarget.Tags.Has("state.alive")
                                || !HasTherapyCondition(health, treatmentTarget))
                            {
                                return new GameActionResult(false);
                            }

                            economy.ChangeWealth(actor, -currentCost);

                            var intellect =
                                stats.GetStats(treatmentTarget)
                                    .First(stat =>
                                        stat.Id.Equals(
                                            "intellect",
                                            StringComparison.OrdinalIgnoreCase))
                                    .Value;

                            var successChance =
                                MedicalTreatmentRules.AdjustSuccessChance(
                                    TherapyRules.GetSuccessChance(intellect),
                                    currentMedical.TreatmentSuccessAdd);

                            var success = random.NextDouble() < successChance;
                            var currentVariant =
                                historical.GetVariant(
                                    "wellbeing.therapy",
                                    actionContext.GameState.Year)
                                ?? canonical;

                            if (success)
                            {
                                foreach (var conditionId in new[]
                                {
                                    "alcoholism",
                                    "depression",
                                    "anxiety",
                                    "drug_dependence",
                                    "burnout"
                                })
                                {
                                    health.RemoveCondition(
                                        treatmentTarget,
                                        conditionId);
                                }

                                events.Publish(
                                    new GameEvent
                                    {
                                        Type = "wellbeing.therapy_success",
                                        Year = actionContext.GameState.Year,
                                        SubjectId = treatmentTarget.Id,
                                        RelatedPersonIds =
                                            actor.Id == treatmentTarget.Id
                                                ? []
                                                : [actor.Id],
                                        Data = new Dictionary<string, string>
                                        {
                                            ["text"] =
                                                $"{family.GetDisplayName(treatmentTarget)} " +
                                                $"{currentVariant.Narrative}; the treatment was successful."
                                        }
                                    });
                            }
                            else
                            {
                                events.Publish(
                                    new GameEvent
                                    {
                                        Type = "wellbeing.therapy_failure",
                                        Year = actionContext.GameState.Year,
                                        SubjectId = treatmentTarget.Id,
                                        RelatedPersonIds =
                                            actor.Id == treatmentTarget.Id
                                                ? []
                                                : [actor.Id],
                                        Data = new Dictionary<string, string>
                                        {
                                            ["text"] =
                                                $"{family.GetDisplayName(treatmentTarget)} " +
                                                $"{currentVariant.Narrative}, but the treatment was unproductive."
                                        }
                                    });
                            }

                            return new GameActionResult(true);
                        }
                    }
                ];
            });
    }

    private static void RegisterHeal(
        IActionRegistry actions,
        IFamilyService family,
        IHealthService health,
        IEconomyService economy,
        IGameEventBus events,
        IGameState gameState,
        IHistoricalActionVariantService historical,
        HealthcareEraCatalog healthcareEras,
        ILocationService locations,
        ITownFacilityQualityService facilityQuality)
    {
        actions.RegisterDynamicProvider(
            (_, target) =>
            {
                var variant =
                    historical.GetVariant(
                        "wellbeing.heal_relative",
                        gameState.Year)
                    ?? throw new InvalidDataException(
                        $"Missing historical action data for 'wellbeing.heal_relative' in {gameState.Year}.");
                var medical = GetMedicalQuality(
                    target,
                    gameState.Year,
                    locations,
                    facilityQuality);
                var visitingPhysician =
                    IsVisitingPhysicianVariant(variant);
                var localTreatmentCost =
                    visitingPhysician
                        ? HealCost
                        : MedicalTreatmentRules.AdjustCost(
                            HealCost,
                            medical.TreatmentCostMultiplier);

                return
                [
                    new GameActionDefinition
                    {
                        Id =
                            "wellbeing.heal_relative",

                        Label =
                            FormatTreatmentCostLabel(
                                variant.Label,
                                localTreatmentCost),

                        Description =
                            FormatTreatmentCostDescription(
                                variant.Description,
                                localTreatmentCost),

                        Mode =
                            ActionExecutionMode.Queued,

                        QueuePhase =
                            YearPhase.QueuedActionsEarly,

                        IsAvailable =
                            actionContext =>
                            {
                                var actor =
                                    actionContext.Actor;

                                var target =
                                    actionContext.Target;

                                if (!CanActorAct(actionContext)
                                    || !target.Tags.Has(
                                        "state.alive"))
                                {
                                    return false;
                                }

                                var targetHealth =
                                    health.GetHealth(target);

                                if (targetHealth.Current
                                    >= targetHealth.Maximum)
                                {
                                    return false;
                                }

                                var currentVariant =
                                    historical.GetVariant(
                                        "wellbeing.heal_relative",
                                        actionContext.GameState.Year)
                                    ?? variant;
                                var currentMedical = GetMedicalQuality(
                                    target,
                                    actionContext.GameState.Year,
                                    locations,
                                    facilityQuality);
                                var currentVisitingPhysician =
                                    IsVisitingPhysicianVariant(currentVariant);
                                var currentTreatmentCost =
                                    currentVisitingPhysician
                                        ? HealCost
                                        : MedicalTreatmentRules.AdjustCost(
                                            HealCost,
                                            currentMedical.TreatmentCostMultiplier);

                                return (currentVisitingPhysician
                                        || currentMedical.IsAvailable)
                                    && economy.CanAfford(
                                        actor,
                                        currentTreatmentCost);
                            },

                        Execute =
                            actionContext =>
                            {
                                var actor =
                                    actionContext.Actor;

                                var target =
                                    actionContext.Target;

                                var targetHealth =
                                    health.GetHealth(target);

                                var currentVariant =
                                    historical.GetVariant(
                                        "wellbeing.heal_relative",
                                        actionContext.GameState.Year)
                                    ?? variant;
                                var currentMedical = GetMedicalQuality(
                                    target,
                                    actionContext.GameState.Year,
                                    locations,
                                    facilityQuality);
                                var currentVisitingPhysician =
                                    IsVisitingPhysicianVariant(currentVariant);
                                var treatmentCost =
                                    currentVisitingPhysician
                                        ? HealCost
                                        : MedicalTreatmentRules.AdjustCost(
                                            HealCost,
                                            currentMedical.TreatmentCostMultiplier);

                                if ((!currentVisitingPhysician
                                        && !currentMedical.IsAvailable)
                                    || !economy.CanAfford(
                                        actor,
                                        treatmentCost)
                                    || !target.Tags.Has(
                                        "state.alive")
                                    || targetHealth.Current
                                        >= targetHealth.Maximum)
                                {
                                    return new GameActionResult(
                                        false);
                                }

                                economy.ChangeWealth(
                                    actor,
                                    -treatmentCost);

                                var healAmount =
                                    healthcareEras.GetHealAmount(
                                        actionContext.GameState.Year);

                                health.ChangeHealth(
                                    target,
                                    healAmount);

                                var targetPhrase =
                                    actor.Id == target.Id
                                        ? string.Empty
                                        : $" for {family.GetDisplayName(target)}";

                                events.Publish(
                                    new GameEvent
                                    {
                                        Type =
                                            "wellbeing.heal",

                                        Year =
                                            actionContext.GameState.Year,

                                        SubjectId =
                                            actor.Id,

                                        RelatedPersonIds =
                                            [target.Id],

                                        Data =
                                            new Dictionary<string, string>
                                            {
                                                ["healAmount"] =
                                                    healAmount.ToString(
                                                        System.Globalization.CultureInfo.InvariantCulture),

                                                ["text"] =
                                                    $"{family.GetDisplayName(actor)} " +
                                                    $"{currentVariant.Narrative}{targetPhrase}."
                                            }
                                    });

                                return new GameActionResult(
                                    true);
                            }
                    }
                ];
            });
    }

    private static bool IsVisitingPhysicianVariant(
        HistoricalActionVariant variant) =>
        variant.Label.Contains(
            "Summon a Physician",
            StringComparison.OrdinalIgnoreCase);

    private static MedicalQualityInfo GetMedicalQuality(
        IPerson target,
        int year,
        ILocationService locations,
        ITownFacilityQualityService facilityQuality)
    {
        var town = locations.GetLocation(target).HomeTown;
        return facilityQuality.GetMedicalQuality(town, year);
    }

    private static string FormatTreatmentCostLabel(
        string label,
        decimal cost)
    {
        var open = label.LastIndexOf(" (", StringComparison.Ordinal);
        if (open >= 0 && label.EndsWith(" zł)", StringComparison.Ordinal))
            label = label[..open];

        return $"{label} ({cost:N0} zł)";
    }

    private static string FormatTreatmentCostDescription(
        string description,
        decimal cost) =>
        description.Replace(
            "3,000 zł",
            $"{cost:N0} zł",
            StringComparison.Ordinal);

    private static bool HasTherapyCondition(
        IHealthService health,
        IPerson target)
    {
        return health.GetHealth(target).Conditions
            .Any(condition =>
                TherapyRules.IsTreatableCondition(
                    condition.Id));
    }

    private static bool CanActorActOnSelf(
        GameActionContext context)
    {
        return context.Actor.Id
                == context.Target.Id
            && CanActorAct(context);
    }

    private static bool CanActorAct(
        GameActionContext context)
    {
        return context.Actor.Tags.Has(
                "state.alive")
            && context.ActorHasControl
            && !context.Actor.Tags.Has(
                "state.imprisoned");
    }

}
