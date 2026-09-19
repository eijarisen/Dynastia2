using Dynastia.Contracts;

namespace Dynastia.Mechanics.Wellbeing;

public sealed partial class WellbeingPlugin
{
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
                    "Take the year easier. Improves job satisfaction and adds +15 to this year's health calculation, but reduces salary by 10-50% for the year. May slightly improve children's Happiness.",

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

                        foreach (var tag in actor.Tags.All
                            .Where(tag => tag.StartsWith(
                                "modifier.salary.recover.",
                                StringComparison.OrdinalIgnoreCase))
                            .ToList())
                        {
                            actor.Tags.Remove(tag);
                        }

                        var incomeReductionPercent =
                            random.NextInt(10, 50);

                        actor.Tags.Add(
                            $"modifier.salary.recover.{incomeReductionPercent}");

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
                                        ["incomeReductionPercent"] =
                                            incomeReductionPercent.ToString(),

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
        IHealthService health,
        IStressService stress,
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
                    "Use alcohol to blunt current stress. Reduces Stress by 2 " +
                    "for this year, immediately costs 10 Health, and carries " +
                    "a 20% base chance of developing Alcoholism.",

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

                        return actionContext.Actor.Age >= 18
                            && stress.GetStress(actionContext.Actor).Total > 0;
                    },

                Execute =
                    actionContext =>
                    {
                        var actor =
                            actionContext.Actor;

                        if (actor.Age < 18
                            || stress.GetStress(actor).Total <= 0)
                        {
                            return new GameActionResult(
                                false);
                        }

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
                                        ["stressRelief"] = "2",
                                        ["text"] =
                                            $"{family.GetDisplayName(actor)} " +
                                            "drank to take the edge off mounting stress."
                                    }
                            });

                        var alcoholismChance =
                            PersonalityInfluence.AdjustProbability(
                                AlcoholismChanceFromDrinking,
                                actor,
                                melancholic: 0.20,
                                phlegmatic: -0.15,
                                sanguine: -0.10,
                                choleric: 0.20,
                                good: -0.10,
                                evil: 0.10);

                        if (random.NextDouble()
                            < alcoholismChance
                            && !health.HasCondition(
                                actor,
                                "alcoholism"))
                        {
                            health.AddCondition(
                                actor,
                                "alcoholism",
                                actionContext.GameState.Year);

                            var alcoholismName =
                                health.GetHealth(actor).Conditions
                                    .First(condition => condition.Id.Equals(
                                        "alcoholism",
                                        StringComparison.OrdinalIgnoreCase))
                                    .Name;

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
                                                alcoholismName,

                                            ["text"] =
                                                $"{family.GetDisplayName(actor)} " +
                                                $"has developed {alcoholismName}."
                                        }
                                });
                        }

                        return new GameActionResult(
                            true);
                    }
            });
    }

}
