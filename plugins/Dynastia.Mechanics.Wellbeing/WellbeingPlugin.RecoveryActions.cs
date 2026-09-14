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
