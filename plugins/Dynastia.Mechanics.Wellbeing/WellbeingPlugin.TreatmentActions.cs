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
                        Presentation = new()
                        {
                            Emoji = "😊",
                            Categories = [ActionPresentationCategories.Personal, ActionPresentationCategories.Family],
                            AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth,
                            GroupOrder = 20
                        },
                        Label = variant.Label,
                        Description = variant.Description,
                        DisplayCost = treatmentCost,
                        Mode = ActionExecutionMode.Queued,
                        QueuePhase = YearPhase.QueuedActionsEarly,

                        EvaluateAvailability = actionContext =>
                        {
                            var historicallyAvailable =
                                historical.GetVariant(
                                    "wellbeing.therapy",
                                    actionContext.ScheduledExecutionYear)
                                is not null;

                            if (!historicallyAvailable
                                && !ActionCompatibilityParameters.IsRestoredQueuedAction(
                                    actionContext.Parameters))
                            {
                                return ActionEvaluationResult.Denied(
                                    ActionReasonCodes.NoLongerEligible,
                                    "Psychotherapy is not available in this period.");
                            }

                            if (!CanTreatHouseholdMember(actionContext, economy)
                                || !HasTherapyCondition(health, actionContext.Target))
                            {
                                return ActionEvaluationResult.Denied(
                                    ActionReasonCodes.NoLongerEligible,
                                    "Psychotherapy is not currently relevant for this patient.");
                            }

                            var currentMedical = GetMedicalQuality(
                                actionContext.Target,
                                actionContext.GameState.Year,
                                locations,
                                facilityQuality);
                            if (!currentMedical.IsAvailable)
                            {
                                return ActionEvaluationResult.Denied(
                                    ActionReasonCodes.ResourceUnavailable,
                                    "Psychotherapy requires a local medical facility.");
                            }

                            var currentCost =
                                MedicalTreatmentRules.AdjustCost(
                                    TherapyCost,
                                    currentMedical.TreatmentCostMultiplier);

                            return economy.CanAfford(
                                    actionContext.Actor,
                                    currentCost)
                                ? ActionEvaluationResult.Allowed()
                                : ActionEvaluationResult.Denied(
                                    ActionReasonCodes.InsufficientFunds,
                                    $"The household cannot afford {currentCost:N0} zł for psychotherapy.");
                        },

                        Execute = actionContext =>
                        {
                            var actor = actionContext.Actor;
                            var treatmentTarget = actionContext.Target;

                            var historicallyAvailable =
                                historical.GetVariant(
                                    "wellbeing.therapy",
                                    actionContext.ScheduledExecutionYear)
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
                                || !CanTreatHouseholdMember(actionContext, economy)
                                || !economy.CanAfford(actor, currentCost)
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
                                    "burnout",
                                    "gambling_disorder"
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
                var baseHealAmount = healthcareEras.GetHealAmount(gameState.Year);
                var presentation = GetHealTreatment(
                    variant,
                    medical,
                    baseHealAmount);

                return
                [
                    new GameActionDefinition
                    {
                        Id = "wellbeing.heal_relative",
                        Presentation = new()
                        {
                            Emoji = "❤️‍🩹",
                            Categories = [ActionPresentationCategories.Personal],
                            AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth,
                            GroupOrder = 10
                        },
                        Label = StripTreatmentCostLabel(
                            presentation.Label),
                        Description = FormatHealDescription(
                            presentation.Description,
                            presentation.Cost,
                            baseHealAmount,
                            presentation.HealAmount),
                        DisplayCost = presentation.Cost,
                        Mode = ActionExecutionMode.Queued,
                        QueuePhase = YearPhase.QueuedActionsEarly,

                        EvaluateAvailability = actionContext =>
                        {
                            var actor = actionContext.Actor;
                            var treatmentTarget = actionContext.Target;

                            if (!CanTreatHouseholdMember(actionContext, economy))
                            {
                                return ActionEvaluationResult.Denied(
                                    ActionReasonCodes.NoLongerEligible,
                                    "Medical treatment is not currently available for this patient.");
                            }

                            var targetHealth = health.GetHealth(treatmentTarget);
                            if (targetHealth.Current >= targetHealth.Maximum)
                            {
                                return ActionEvaluationResult.Denied(
                                    ActionReasonCodes.NoLongerEligible,
                                    "This patient does not currently need medical treatment.");
                            }

                            var currentVariant = historical.GetVariant(
                                "wellbeing.heal_relative",
                                actionContext.GameState.Year)
                                ?? variant;
                            var currentMedical = GetMedicalQuality(
                                treatmentTarget,
                                actionContext.GameState.Year,
                                locations,
                                facilityQuality);
                            var currentTreatment = GetHealTreatment(
                                currentVariant,
                                currentMedical,
                                healthcareEras.GetHealAmount(
                                    actionContext.GameState.Year));

                            return economy.CanAfford(
                                    actor,
                                    currentTreatment.Cost)
                                ? ActionEvaluationResult.Allowed()
                                : ActionEvaluationResult.Denied(
                                    ActionReasonCodes.InsufficientFunds,
                                    $"The household cannot afford {currentTreatment.Cost:N0} zł for treatment.");
                        },

                        Execute = actionContext =>
                        {
                            var actor = actionContext.Actor;
                            var treatmentTarget = actionContext.Target;
                            var targetHealth = health.GetHealth(treatmentTarget);
                            var currentVariant = historical.GetVariant(
                                "wellbeing.heal_relative",
                                actionContext.GameState.Year)
                                ?? variant;
                            var currentMedical = GetMedicalQuality(
                                treatmentTarget,
                                actionContext.GameState.Year,
                                locations,
                                facilityQuality);
                            var currentTreatment = GetHealTreatment(
                                currentVariant,
                                currentMedical,
                                healthcareEras.GetHealAmount(
                                    actionContext.GameState.Year));

                            if (!CanTreatHouseholdMember(actionContext, economy)
                                || !economy.CanAfford(actor, currentTreatment.Cost)
                                || targetHealth.Current >= targetHealth.Maximum)
                            {
                                return new GameActionResult(false);
                            }

                            economy.ChangeWealth(
                                actor,
                                -currentTreatment.Cost);
                            health.ChangeHealth(
                                treatmentTarget,
                                currentTreatment.HealAmount);

                            var targetPhrase = actor.Id == treatmentTarget.Id
                                ? string.Empty
                                : $" for {family.GetDisplayName(treatmentTarget)}";

                            events.Publish(
                                new GameEvent
                                {
                                    Type = "wellbeing.heal",
                                    Year = actionContext.GameState.Year,
                                    SubjectId = actor.Id,
                                    RelatedPersonIds = [treatmentTarget.Id],
                                    Data = new Dictionary<string, string>
                                    {
                                        ["healAmount"] = currentTreatment.HealAmount.ToString(
                                            System.Globalization.CultureInfo.InvariantCulture),
                                        ["careSource"] = currentTreatment.IsLocal
                                            ? "local"
                                            : "visiting_physician",
                                        ["text"] =
                                            $"{family.GetDisplayName(actor)} " +
                                            $"{currentTreatment.Narrative}{targetPhrase}."
                                    }
                                });

                            return new GameActionResult(true);
                        }
                    }
                ];
            });
    }

    private static HealTreatment GetHealTreatment(
        HistoricalActionVariant variant,
        MedicalQualityInfo medical,
        double baseHealAmount)
    {
        if (medical.IsAvailable)
        {
            return new HealTreatment(
                variant.Label,
                variant.Description,
                variant.Narrative,
                MedicalTreatmentRules.AdjustCost(
                    HealCost,
                    medical.TreatmentCostMultiplier),
                MedicalTreatmentRules.AdjustHealAmount(
                    baseHealAmount,
                    1.0 + medical.TreatmentSuccessAdd),
                true);
        }

        return new HealTreatment(
            "Summon a Physician",
            "Bring a visiting physician from another town for treatment and convalescence.",
            "summoned a visiting physician from another town and arranged treatment",
            MedicalTreatmentRules.AdjustCost(
                HealCost,
                MedicalTreatmentRules.VisitingPhysicianCostMultiplier),
            MedicalTreatmentRules.AdjustHealAmount(
                baseHealAmount,
                MedicalTreatmentRules.VisitingPhysicianHealMultiplier),
            false);
    }

    private static MedicalQualityInfo GetMedicalQuality(
        IPerson target,
        int year,
        ILocationService locations,
        ITownFacilityQualityService facilityQuality)
    {
        var town = locations.GetLocation(target).HomeTown;
        return facilityQuality.GetMedicalQuality(town, year);
    }

    private static string StripTreatmentCostLabel(
        string label)
    {
        var open = label.LastIndexOf(" (", StringComparison.Ordinal);
        if (open >= 0 && label.EndsWith(" zł)", StringComparison.Ordinal))
            return label[..open];

        return label;
    }

    private static string FormatHealDescription(
        string description,
        decimal cost,
        double baseHealAmount,
        double healAmount)
    {
        var result = FormatTreatmentCostDescription(description, cost);
        result = result.Replace(
            $"Restores {baseHealAmount:0.#} health.",
            $"Restores {healAmount:0.#} health.",
            StringComparison.Ordinal);

        if (!result.Contains("Restores", StringComparison.OrdinalIgnoreCase))
            result += $" Restores {healAmount:0.#} health.";

        return result;
    }

    private static string FormatTreatmentCostDescription(
        string description,
        decimal cost) =>
        description.Replace(
            "3,000 zł",
            $"{cost:N0} zł",
            StringComparison.Ordinal);

    private sealed record HealTreatment(
        string Label,
        string Description,
        string Narrative,
        decimal Cost,
        double HealAmount,
        bool IsLocal);

    private static bool HasTherapyCondition(
        IHealthService health,
        IPerson target)
    {
        return health.GetHealth(target).Conditions
            .Any(condition =>
                TherapyRules.IsTreatableCondition(
                    condition.Id));
    }

    private static bool CanTreatHouseholdMember(
        GameActionContext context,
        IEconomyService economy)
    {
        return CanActorAct(context)
            && HouseholdKinshipRules.IsResidentHouseholdMember(
                context.Actor,
                context.Target,
                economy);
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
