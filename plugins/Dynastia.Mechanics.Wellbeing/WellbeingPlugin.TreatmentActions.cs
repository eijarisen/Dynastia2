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
        IHistoricalActionVariantService historical)
    {
        var canonical =
            historical.GetCanonicalVariant(
                "wellbeing.therapy")
            ?? throw new InvalidDataException(
                "Missing historical action data for 'wellbeing.therapy'.");

        actions.Register(
            new GameActionDefinition
            {
                Id =
                    "wellbeing.therapy",

                Label =
                    canonical.Label,

                Description =
                    canonical.Description,

                Mode =
                    ActionExecutionMode.Queued,

                QueuePhase =
                    YearPhase.QueuedActionsEarly,

                IsAvailable =
                    actionContext =>
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
                            || !actionContext.Target.Tags.Has(
                                "state.alive"))
                        {
                            return false;
                        }

                        if (!HasTherapyCondition(
                            health,
                            actionContext.Target))
                        {
                            return false;
                        }

                        return economy.GetHousehold(
                            actionContext.Actor)
                            ?.Wealth >= TherapyCost;
                    },

                Execute =
                    actionContext =>
                    {
                        var actor =
                            actionContext.Actor;

                        var target =
                            actionContext.Target;

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

                        var household =
                            economy.GetHousehold(
                                actor);

                        if (household is null
                            || household.Wealth < TherapyCost
                            || !target.Tags.Has(
                                "state.alive")
                            || !HasTherapyCondition(
                                health,
                                target))
                        {
                            return new GameActionResult(
                                false);
                        }

                        economy.ChangeWealth(
                            actor,
                            -TherapyCost);

                        var intellect =
                            stats.GetStats(target)
                                .First(
                                    stat =>
                                        stat.Id.Equals(
                                            "intellect",
                                            StringComparison.OrdinalIgnoreCase))
                                .Value;

                        var successChance =
                            TherapyRules.GetSuccessChance(
                                intellect);

                        var success =
                            random.NextDouble()
                            < successChance;

                        var variant =
                            historical.GetVariant(
                                "wellbeing.therapy",
                                actionContext.GameState.Year)
                            ?? canonical;

                        if (success)
                        {
                            health.RemoveCondition(
                                target,
                                "alcoholism");

                            health.RemoveCondition(
                                target,
                                "depression");

                            health.RemoveCondition(
                                target,
                                "anxiety");

                            health.RemoveCondition(
                                target,
                                "drug_dependence");

                            events.Publish(
                                new GameEvent
                                {
                                    Type =
                                        "wellbeing.therapy_success",

                                    Year =
                                        actionContext.GameState.Year,

                                    SubjectId =
                                        target.Id,

                                    RelatedPersonIds =
                                        actor.Id == target.Id
                                            ? []
                                            : [actor.Id],

                                    Data =
                                        new Dictionary<string, string>
                                        {
                                            ["text"] =
                                                $"{family.GetDisplayName(target)} " +
                                                $"{variant.Narrative}; the treatment was successful."
                                        }
                                });
                        }
                        else
                        {
                            events.Publish(
                                new GameEvent
                                {
                                    Type =
                                        "wellbeing.therapy_failure",

                                    Year =
                                        actionContext.GameState.Year,

                                    SubjectId =
                                        target.Id,

                                    RelatedPersonIds =
                                        actor.Id == target.Id
                                            ? []
                                            : [actor.Id],

                                    Data =
                                        new Dictionary<string, string>
                                        {
                                            ["text"] =
                                                $"{family.GetDisplayName(target)} " +
                                                $"{variant.Narrative}, but the treatment was unproductive."
                                        }
                                });
                        }

                        return new GameActionResult(
                            true);
                    }
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
        HealthcareEraCatalog healthcareEras)
    {
        actions.RegisterDynamicProvider(
            (_, _) =>
            {
                var variant =
                    historical.GetVariant(
                        "wellbeing.heal_relative",
                        gameState.Year)
                    ?? throw new InvalidDataException(
                        $"Missing historical action data for 'wellbeing.heal_relative' in {gameState.Year}.");

                return
                [
                    new GameActionDefinition
                    {
                        Id =
                            "wellbeing.heal_relative",

                        Label =
                            variant.Label,

                        Description =
                            variant.Description,

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

                                return economy.GetHousehold(actor)
                                    ?.Wealth >= HealCost;
                            },

                        Execute =
                            actionContext =>
                            {
                                var actor =
                                    actionContext.Actor;

                                var target =
                                    actionContext.Target;

                                var household =
                                    economy.GetHousehold(actor);

                                var targetHealth =
                                    health.GetHealth(target);

                                if (household is null
                                    || household.Wealth < HealCost
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
                                    -HealCost);

                                var healAmount =
                                    healthcareEras.GetHealAmount(
                                        actionContext.GameState.Year);

                                health.ChangeHealth(
                                    target,
                                    healAmount);

                                var currentVariant =
                                    historical.GetVariant(
                                        "wellbeing.heal_relative",
                                        actionContext.GameState.Year)
                                    ?? variant;

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
