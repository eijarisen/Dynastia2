using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class CareerPlugin
{
    private static void RegisterActions(
        IActionRegistry actions,
        StandardCareerService career,
        IStatsService stats,
        IGameRandom random,
        IFamilyService family,
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id = "career.quit_job",
                Label = "Quit the Job",
                Description =
                    "Leave employment. Because this resolves after finances, this year's salary is still paid.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.LifeEvents,

                IsAvailable = actionContext =>
                {
                    if (actionContext.Actor.Id != actionContext.Target.Id
                        || !actionContext.Actor.Tags.Has("state.alive")
                        || !actionContext.Actor.Tags.Has("control.playable"))
                    {
                        return false;
                    }

                    var current =
                        career.GetCareer(actionContext.Actor);

                    return !current.IsRetired
                        && current.JobLevel > 0;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var current = career.GetCareer(actor);

                    if (current.IsRetired
                        || current.JobLevel <= 0)
                    {
                        return new GameActionResult(false);
                    }

                    career.SetJobLevel(actor, 0);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "career.quit",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["careerId"] =
                                    current.CareerId
                                    ?? string.Empty,

                                ["careerName"] =
                                    current.CareerName
                                    ?? string.Empty,

                                ["jobTitle"] =
                                    current.JobTitle,

                                ["text"] =
                                    $"{family.GetDisplayName(actor)} quit their job as {current.JobTitle}."
                            }
                        });

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "career.work_harder",
                Label = "Work Harder",
                Description =
                    "Push harder at work. Greatly improves this year's promotion chance and usually brings 10-50% extra salary home, although there is a small chance the extra effort is unpaid. Costs 5 health and may slightly reduce children's Happiness.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    if (actionContext.Actor.Id != actionContext.Target.Id
                        || !actionContext.Actor.Tags.Has("state.alive")
                        || !actionContext.Actor.Tags.Has("control.playable")
                        || actionContext.Actor.Age < 18)
                    {
                        return false;
                    }

                    var current =
                        career.GetCareer(actionContext.Actor);

                    return !current.IsRetired
                        && current.JobLevel > 0
                        && current.JobLevel < 5;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;

                    actor.Tags.Add(
                        "modifier.work_harder");

                    foreach (var tag in actor.Tags.All
                        .Where(tag => tag.StartsWith(
                            "modifier.salary.work_harder.",
                            StringComparison.OrdinalIgnoreCase))
                        .ToList())
                    {
                        actor.Tags.Remove(tag);
                    }

                    var bonusPercent =
                        random.NextDouble() < 0.10
                            ? 0
                            : random.NextInt(10, 50);

                    if (bonusPercent > 0)
                    {
                        actor.Tags.Add(
                            $"modifier.salary.work_harder.{bonusPercent}");
                    }

                    events.Publish(
                        new GameEvent
                        {
                            Type = "career.work_harder",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["bonusPercent"] =
                                    bonusPercent.ToString(),
                                ["suppressChronicle"] = "true",
                                ["text"] =
                                    bonusPercent > 0
                                        ? $"{family.GetDisplayName(actor)} worked harder and earned {bonusPercent}% extra salary."
                                        : $"{family.GetDisplayName(actor)} worked harder, but the extra effort brought no additional pay."
                            }
                        });

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "career.seek_employment",
                Label = "Seek Employment",
                Description =
                    "Browse concrete vacancies in the current town and apply for one. " +
                    "Your relevant ability, education and work history influence the application.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.LifeEvents,

                IsAvailable = actionContext =>
                {
                    if (actionContext.Actor.Id != actionContext.Target.Id
                        || !actionContext.Actor.Tags.Has("state.alive")
                        || !actionContext.Actor.Tags.Has("control.playable")
                        || actionContext.Actor.Age < 18)
                    {
                        return false;
                    }

                    var current = career.GetCareer(actionContext.Actor);
                    return !current.IsRetired
                        && current.JobLevel == 0;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var current = career.GetCareer(actor);

                    if (current.IsRetired
                        || current.JobLevel != 0)
                    {
                        return new GameActionResult(false);
                    }

                    if (TryGetSelectedJob(actionContext, out _, out _))
                    {
                        return ResolveSelectedJobApplication(
                            actionContext,
                            career,
                            family,
                            events,
                            actor);
                    }

                    // Compatibility for a job search queued by an older save.
                    if (!ActionCompatibilityParameters.IsRestoredQueuedAction(
                            actionContext.Parameters))
                    {
                        return new GameActionResult(false);
                    }

                    var opportunity = career.CreateEmploymentOpportunity(
                        actor,
                        stats);
                    var successChance = PersonalityInfluence.AdjustProbability(
                        opportunity.SuccessChance,
                        actor,
                        sanguine: 0.10);

                    if (random.NextDouble() < successChance)
                    {
                        career.AcceptEmploymentOpportunity(actor, opportunity);
                        var employed = career.GetCareer(actor);

                        events.Publish(new GameEvent
                        {
                            Type = "career.employment",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["careerId"] = employed.CareerId ?? string.Empty,
                                ["careerName"] = employed.CareerName ?? string.Empty,
                                ["jobTitle"] = employed.JobTitle,
                                ["text"] =
                                    $"{family.GetDisplayName(actor)} found employment as {employed.JobTitle}."
                            }
                        });
                    }

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "career.find_another_job",
                Label = "Find a Better Job",
                Description =
                    "Browse the same local vacancies while keeping your current job. " +
                    "A successful offer is accepted only when it pays more than your present position.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.LifeEvents,

                IsAvailable = actionContext =>
                {
                    if (actionContext.Actor.Id != actionContext.Target.Id
                        || !actionContext.Actor.Tags.Has("state.alive")
                        || !actionContext.Actor.Tags.Has("control.playable")
                        || actionContext.Actor.Age < 18
                        || actionContext.Actor.Tags.Has("state.imprisoned"))
                    {
                        return false;
                    }

                    var current = career.GetCareer(actionContext.Actor);
                    return !current.IsRetired
                        && current.JobLevel > 0
                        && current.JobLevel < 4;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var before = career.GetCareer(actor);

                    if (TryGetSelectedJob(actionContext, out _, out _))
                    {
                        return ResolveSelectedJobApplication(
                            actionContext,
                            career,
                            family,
                            events,
                            actor);
                    }

                    if (!ActionCompatibilityParameters.IsRestoredQueuedAction(
                            actionContext.Parameters))
                    {
                        return new GameActionResult(false);
                    }

                    var changed = career.TryFindBetterJob(actor);
                    if (!changed)
                        return new GameActionResult(true);

                    var after = career.GetCareer(actor);
                    events.Publish(new GameEvent
                    {
                        Type = "career.changed_job",
                        Year = actionContext.GameState.Year,
                        SubjectId = actor.Id,
                        Data = new Dictionary<string, string>
                        {
                            ["oldCareerId"] = before.CareerId ?? string.Empty,
                            ["newCareerId"] = after.CareerId ?? string.Empty,
                            ["text"] =
                                $"{family.GetDisplayName(actor)} left {before.JobTitle} work " +
                                $"for a better-paying position as {after.JobTitle}."
                        }
                    });

                    return new GameActionResult(true);
                }
            });
    }
}
