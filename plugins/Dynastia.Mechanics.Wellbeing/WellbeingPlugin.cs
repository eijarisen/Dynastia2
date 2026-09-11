using Dynastia.Contracts;

namespace Dynastia.Mechanics.Wellbeing;

public sealed class WellbeingPlugin : IGamePlugin
{
    private const decimal TherapyCost =
        1500m;

    private const decimal HealCost =
        1000m;

    private const double HealAmount =
        25;

    private const double DrinkHealthPenalty =
        10;

    private const double AlcoholismChanceFromDrinking =
        0.20;

    private const double TherapySuccessIntellectFactor =
        6;

    private const double TherapySuccessDivisor =
        10;

    public void Initialize(
        IGamePluginContext context)
    {
        var family =
            context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var health =
            context.GetService<IHealthService>()
            ?? throw new InvalidOperationException(
                "Health service is unavailable.");

        var career =
            context.GetService<ICareerService>()
            ?? throw new InvalidOperationException(
                "Career service is unavailable.");

        var economy =
            context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException(
                "Economy service is unavailable.");

        var stats =
            context.GetService<IStatsService>()
            ?? throw new InvalidOperationException(
                "Stats service is unavailable.");

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Random service is unavailable.");

        var data =
            context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException(
                "Game data service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var actions =
            context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException(
                "Action registry is unavailable.");

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        var healthModifiers =
            context.GetService<IAnnualHealthModifierRegistry>()
            ?? throw new InvalidOperationException(
                "Annual health modifier registry is unavailable.");

        var recoveryActivities =
            RecoveryActivityCatalog.Load(
                data);

        healthModifiers.Register(
            new WellbeingHealthModifierProvider());

        RegisterRecover(
            actions,
            family,
            career,
            random,
            events,
            recoveryActivities);

        RegisterDrink(
            actions,
            family,
            career,
            health,
            random,
            events);

        RegisterTherapy(
            actions,
            family,
            health,
            economy,
            stats,
            random,
            events);

        RegisterHeal(
            actions,
            family,
            health,
            economy,
            events);

        systems.Register(
            new WellbeingCleanupYearSystem());

        context.Log(
            "Wellbeing mechanics registered.");
    }

    private static void RegisterRecover(
        IActionRegistry actions,
        IFamilyService family,
        ICareerService career,
        IGameRandom random,
        IGameEventBus events,
        RecoveryActivityCatalog recoveryActivities)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id =
                    "wellbeing.recover",

                Label =
                    "Recover",

                Description =
                    "Take the year easier. Improves job satisfaction " +
                    "and adds +20 to this year's health calculation.",

                Mode =
                    ActionExecutionMode.Queued,

                QueuePhase =
                    YearPhase.QueuedActionsEarly,

                IsAvailable =
                    actionContext =>
                    {
                        if (!CanActorActOnSelf(
                            actionContext))
                        {
                            return false;
                        }

                        return !career
                            .GetCareer(
                                actionContext.Actor)
                            .IsRetired;
                    },

                Execute =
                    actionContext =>
                    {
                        var actor =
                            actionContext.Actor;

                        if (career
                            .GetCareer(actor)
                            .IsRetired)
                        {
                            return new GameActionResult(
                                false);
                        }

                        career.ChangeJobSatisfaction(
                            actor,
                            1);

                        actor.Tags.Add(
                            "modifier.recover");

                        var activity =
                            recoveryActivities.Select(
                                actionContext.GameState.Year,
                                random);

                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "wellbeing.recover",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    actor.Id,

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["text"] =
                                            $"{family.GetDisplayName(actor)} " +
                                            $"{activity.Text}."
                                    }
                            });

                        return new GameActionResult(
                            true);
                    }
            });
    }

    private static void RegisterDrink(
        IActionRegistry actions,
        IFamilyService family,
        ICareerService career,
        IHealthService health,
        IGameRandom random,
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id =
                    "wellbeing.drink",

                Label =
                    "Drink",

                Description =
                    "Turn to alcohol to cope with a miserable job. " +
                    "Job satisfaction +2, immediate health -10, " +
                    "with a 20% chance of developing Alcoholism.",

                Mode =
                    ActionExecutionMode.Queued,

                QueuePhase =
                    YearPhase.QueuedActionsEarly,

                IsAvailable =
                    actionContext =>
                    {
                        if (!CanActorActOnSelf(
                            actionContext))
                        {
                            return false;
                        }

                        var current =
                            career.GetCareer(
                                actionContext.Actor);

                        return !current.IsRetired
                            && current.JobLevel > 0
                            && current.JobSatisfaction == 1;
                    },

                Execute =
                    actionContext =>
                    {
                        var actor =
                            actionContext.Actor;

                        var current =
                            career.GetCareer(actor);

                        if (current.IsRetired
                            || current.JobLevel <= 0
                            || current.JobSatisfaction != 1)
                        {
                            return new GameActionResult(
                                false);
                        }

                        career.ChangeJobSatisfaction(
                            actor,
                            2);

                        health.ChangeHealth(
                            actor,
                            -DrinkHealthPenalty);

                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "wellbeing.drink",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    actor.Id,

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["text"] =
                                            $"{family.GetDisplayName(actor)} " +
                                            "spent the year drinking to cope with stress."
                                    }
                            });

                        var alcoholismChance =
                            PersonalityInfluence.AdjustProbability(
                                AlcoholismChanceFromDrinking,
                                actor,
                                melancholic: 0.15,
                                choleric: 0.15,
                                good: -0.10);

                        if (random.NextDouble()
                            < alcoholismChance
                            && !health.HasCondition(
                                actor,
                                "alcoholism"))
                        {
                            health.AddCondition(
                                actor,
                                "alcoholism");

                            events.Publish(
                                new GameEvent
                                {
                                    Type =
                                        "health.serious_illness",

                                    Year =
                                        actionContext.GameState.Year,

                                    SubjectId =
                                        actor.Id,

                                    Data =
                                        new Dictionary<string, string>
                                        {
                                            ["conditionId"] =
                                                "alcoholism",

                                            ["condition"] =
                                                "Alcoholism",

                                            ["text"] =
                                                $"{family.GetDisplayName(actor)} " +
                                                "has developed alcoholism."
                                        }
                                });
                        }

                        return new GameActionResult(
                            true);
                    }
            });
    }

    private static void RegisterTherapy(
        IActionRegistry actions,
        IFamilyService family,
        IHealthService health,
        IEconomyService economy,
        IStatsService stats,
        IGameRandom random,
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id =
                    "wellbeing.therapy",

                Label =
                    "Go to Therapy ($1,500)",

                Description =
                    "Treat Alcoholism, Depression and Anxiety. " +
                    "The fee is paid even if therapy fails.",

                Mode =
                    ActionExecutionMode.Queued,

                QueuePhase =
                    YearPhase.QueuedActionsEarly,

                IsAvailable =
                    actionContext =>
                    {
                        if (!CanActorAct(
                            actionContext.Actor)
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
                            (TherapySuccessIntellectFactor
                             - intellect)
                            / TherapySuccessDivisor;

                        var success =
                            random.NextDouble()
                            < successChance;

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
                                                "underwent successful therapy and feels mentally stronger."
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
                                                "went to therapy, but the session was unproductive."
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
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id =
                    "wellbeing.heal_relative",

                Label =
                    "Heal Selected Relative ($1,000)",

                Description =
                    "Pay for medical treatment for your current spouse " +
                    "or one of your children. Restores 25 health.",

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

                        if (!CanActorAct(actor)
                            || target.Id == actor.Id
                            || !target.Tags.Has(
                                "state.alive"))
                        {
                            return false;
                        }

                        var validRelative =
                            family.GetSpouse(actor)
                                ?.Id == target.Id
                            || family.GetChildren(actor)
                                .Any(
                                    child =>
                                        child.Id == target.Id);

                        if (!validRelative)
                            return false;

                        if (health.GetHealth(target)
                            .Current >= 90)
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

                        var validRelative =
                            family.GetSpouse(actor)
                                ?.Id == target.Id
                            || family.GetChildren(actor)
                                .Any(
                                    child =>
                                        child.Id == target.Id);

                        if (household is null
                            || household.Wealth < HealCost
                            || !validRelative
                            || !target.Tags.Has(
                                "state.alive")
                            || health.GetHealth(target)
                                .Current >= 90)
                        {
                            return new GameActionResult(
                                false);
                        }

                        economy.ChangeWealth(
                            actor,
                            -HealCost);

                        health.ChangeHealth(
                            target,
                            HealAmount);

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
                                        ["text"] =
                                            $"{family.GetDisplayName(actor)} " +
                                            $"paid for medical treatment for " +
                                            $"{family.GetDisplayName(target)}, " +
                                            "improving their health."
                                    }
                            });

                        return new GameActionResult(
                            true);
                    }
            });
    }

    private static bool HasTherapyCondition(
        IHealthService health,
        IPerson target)
    {
        return health.HasCondition(
                target,
                "alcoholism")
            || health.HasCondition(
                target,
                "depression")
            || health.HasCondition(
                target,
                "anxiety");
    }

    private static bool CanActorActOnSelf(
        GameActionContext context)
    {
        return context.Actor.Id
                == context.Target.Id
            && CanActorAct(
                context.Actor);
    }

    private static bool CanActorAct(
        IPerson actor)
    {
        return actor.Tags.Has(
                "state.alive")
            && actor.Tags.Has(
                "control.playable")
            && !actor.Tags.Has(
                "state.imprisoned");
    }
}
