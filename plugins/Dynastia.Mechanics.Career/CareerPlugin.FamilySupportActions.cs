using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class CareerPlugin
{
    private static void RegisterFamilySupportActions(
        IActionRegistry actions,
        StandardCareerService career,
        IStatsService stats,
        IHealthService health,
        IGameRandom random,
        IFamilyService family,
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id = "career.help_seek_employment",
                Label = "Help to Seek Employment",
                Description =
                    "Browse vacancies for your unemployed spouse or unmarried adult daughter " +
                    "and help them apply for a specific position.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || family.GetSex(target) != Sex.Female
                        || target.Age < 18)
                    {
                        return false;
                    }

                    var targetCareer = career.GetCareer(target);
                    if (targetCareer.IsRetired
                        || targetCareer.IsEmployed)
                    {
                        return false;
                    }

                    var isWife = family.GetSpouse(actor)?.Id == target.Id;
                    var isUnmarriedDaughter = family.GetChildren(actor)
                        .Any(child => child.Id == target.Id)
                        && family.GetSpouse(target) is null;

                    return isWife || isUnmarriedDaughter;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || family.GetSex(target) != Sex.Female
                        || target.Age < 18)
                    {
                        return new GameActionResult(false);
                    }

                    var targetCareer = career.GetCareer(target);
                    var isWife = family.GetSpouse(actor)?.Id == target.Id;
                    var isUnmarriedDaughter = family.GetChildren(actor)
                        .Any(child => child.Id == target.Id)
                        && family.GetSpouse(target) is null;

                    if ((!isWife && !isUnmarriedDaughter)
                        || targetCareer.IsRetired
                        || targetCareer.IsEmployed)
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
                            target,
                            actor);
                    }

                    if (!ActionCompatibilityParameters.IsRestoredQueuedAction(
                            actionContext.Parameters))
                    {
                        return new GameActionResult(false);
                    }

                    var opportunity = career.CreateEmploymentOpportunity(
                        target,
                        stats);
                    var successChance = PersonalityInfluence.AdjustProbability(
                        opportunity.SuccessChance,
                        target,
                        sanguine: 0.10);

                    if (random.NextDouble() < successChance)
                    {
                        career.AcceptEmploymentOpportunity(target, opportunity);
                        var employed = career.GetCareer(target);

                        events.Publish(new GameEvent
                        {
                            Type = "career.employment",
                            Year = actionContext.GameState.Year,
                            SubjectId = target.Id,
                            RelatedPersonIds = [actor.Id],
                            Data = new Dictionary<string, string>
                            {
                                ["careerId"] = employed.CareerId ?? string.Empty,
                                ["careerName"] = employed.CareerName ?? string.Empty,
                                ["jobTitle"] = employed.JobTitle,
                                ["text"] =
                                    $"{family.GetDisplayName(target)} found employment as {employed.JobTitle}."
                            }
                        });
                    }

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "career.help_find_better_job",
                Label = "Find a Better Job",
                Description =
                    "Browse better-paying vacancies for your employed spouse or unmarried adult daughter.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || family.GetSex(target) != Sex.Female
                        || target.Age < 18
                        || target.Tags.Has("state.imprisoned"))
                    {
                        return false;
                    }

                    var isWife = family.GetSpouse(actor)?.Id == target.Id;
                    var isUnmarriedDaughter = family.GetChildren(actor)
                        .Any(child => child.Id == target.Id)
                        && family.GetSpouse(target) is null;
                    if (!isWife && !isUnmarriedDaughter)
                        return false;

                    var targetCareer = career.GetCareer(target);
                    return !targetCareer.IsRetired
                        && targetCareer.IsEmployed
                        && (targetCareer.IsSelfEmployed || targetCareer.JobLevel < 3);
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || family.GetSex(target) != Sex.Female
                        || target.Age < 18)
                    {
                        return new GameActionResult(false);
                    }

                    var isWife = family.GetSpouse(actor)?.Id == target.Id;
                    var isUnmarriedDaughter = family.GetChildren(actor)
                        .Any(child => child.Id == target.Id)
                        && family.GetSpouse(target) is null;
                    var targetCareer = career.GetCareer(target);

                    if ((!isWife && !isUnmarriedDaughter)
                        || targetCareer.IsRetired
                        || !targetCareer.IsEmployed
                        || !targetCareer.IsSelfEmployed && targetCareer.JobLevel >= 3)
                    {
                        return new GameActionResult(false);
                    }

                    return TryGetSelectedJob(actionContext, out _, out _)
                        ? ResolveSelectedJobApplication(
                            actionContext,
                            career,
                            family,
                            events,
                            target,
                            actor)
                        : new GameActionResult(false);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "career.ask_to_recover",
                Label = "Ask to Recover",
                Description =
                    "Ask your unhappy employed spouse or miserable adult daughter to take a year easier. " +
                    "There is a 50% refusal chance. On success their salary is reduced by 10-50% for the year.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id)
                    {
                        return false;
                    }

                    if (!IsRecoverOrQuitTarget(
                        actor,
                        target,
                        career,
                        family))
                    {
                        return false;
                    }

                    var targetCareer =
                        career.GetCareer(target);

                    var maximumSatisfaction =
                        family.GetSpouse(actor)?.Id == target.Id
                            ? 2
                            : 1;

                    return targetCareer.IsEmployed
                        && targetCareer.JobSatisfaction
                            <= maximumSatisfaction;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || !IsRecoverOrQuitTarget(
                            actor,
                            target,
                            career,
                            family))
                    {
                        return new GameActionResult(false);
                    }

                    var targetCareer =
                        career.GetCareer(target);

                    var maximumSatisfaction =
                        family.GetSpouse(actor)?.Id == target.Id
                            ? 2
                            : 1;

                    if (!targetCareer.IsEmployed
                        || targetCareer.JobSatisfaction
                            > maximumSatisfaction)
                    {
                        return new GameActionResult(false);
                    }

                    if (random.NextDouble() > 0.5)
                    {
                        career.ChangeJobSatisfaction(
                            target,
                            2);

                        // Requested recovery restores 15 health immediately.
                        // Annual health processing later caps any overflow.
                        health.ChangeHealthUnclamped(
                            target,
                            15);

                        foreach (var tag in target.Tags.All
                            .Where(tag => tag.StartsWith(
                                "modifier.salary.recover.",
                                StringComparison.OrdinalIgnoreCase))
                            .ToList())
                        {
                            target.Tags.Remove(tag);
                        }

                        var incomeReductionPercent =
                            random.NextInt(10, 50);

                        target.Tags.Add(
                            $"modifier.salary.recover.{incomeReductionPercent}");

                        events.Publish(
                            new GameEvent
                            {
                                Type = "career.ask_recover_success",
                                Year = actionContext.GameState.Year,
                                SubjectId = actor.Id,
                                RelatedPersonIds = [target.Id],
                                Data = new Dictionary<string, string>
                                {
                                    ["incomeReductionPercent"] =
                                        incomeReductionPercent.ToString(),
                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} convinced " +
                                        $"{family.GetDisplayName(target)} to take a year off to recover."
                                }
                            });
                    }
                    else
                    {
                        events.Publish(
                            new GameEvent
                            {
                                Type = "career.ask_recover_failure",
                                Year = actionContext.GameState.Year,
                                SubjectId = actor.Id,
                                RelatedPersonIds = [target.Id],
                                Data = new Dictionary<string, string>
                                {
                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} asked " +
                                        $"{family.GetDisplayName(target)} to take a break, but they refused."
                                }
                            });
                    }

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "career.ask_to_quit",
                Label = "Ask to Quit Job",
                Description =
                    "Ask your miserable employed spouse or adult daughter to quit. " +
                    "There is a 50% refusal chance.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || !IsRecoverOrQuitTarget(
                            actor,
                            target,
                            career,
                            family))
                    {
                        return false;
                    }

                    var targetCareer =
                        career.GetCareer(target);

                    return targetCareer.IsEmployed
                        && targetCareer.JobSatisfaction == 1;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor)
                        || !target.Tags.Has("state.alive")
                        || !IsRecoverOrQuitTarget(
                            actor,
                            target,
                            career,
                            family))
                    {
                        return new GameActionResult(false);
                    }

                    var targetCareer =
                        career.GetCareer(target);

                    if (!targetCareer.IsEmployed
                        || targetCareer.JobSatisfaction != 1)
                    {
                        return new GameActionResult(false);
                    }

                    if (random.NextDouble() > 0.5)
                    {
                        career.SetJobLevel(
                            target,
                            0);

                        events.Publish(
                            new GameEvent
                            {
                                Type = "career.ask_quit_success",
                                Year = actionContext.GameState.Year,
                                SubjectId = actor.Id,
                                RelatedPersonIds = [target.Id],
                                Data = new Dictionary<string, string>
                                {
                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} asked " +
                                        $"{family.GetDisplayName(target)} to quit their job, and they agreed."
                                }
                            });
                    }
                    else
                    {
                        events.Publish(
                            new GameEvent
                            {
                                Type = "career.ask_quit_failure",
                                Year = actionContext.GameState.Year,
                                SubjectId = actor.Id,
                                RelatedPersonIds = [target.Id],
                                Data = new Dictionary<string, string>
                                {
                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} asked " +
                                        $"{family.GetDisplayName(target)} to quit their job, but they refused."
                                }
                            });
                    }

                    return new GameActionResult(true);
                }
            });
    }

}
