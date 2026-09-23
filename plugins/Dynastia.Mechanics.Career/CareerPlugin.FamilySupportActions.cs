using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class CareerPlugin
{
    private static void RegisterFamilySupportActions(
        IActionRegistry actions,
        StandardCareerService career,
        IStatsService stats,
        IGameRandom random,
        IFamilyService family,
        IEconomyService economy,
        IGameEventBus events,
        Func<ICraftService?> craftResolver,
        Func<IFarmingService?> farmingResolver)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id = "career.help_seek_employment",
                Presentation = new()
                {
                    Emoji = "✅",
                    Categories = [ActionPresentationCategories.Career, ActionPresentationCategories.Family],
                    PlacementAnchor = ActionPresentationGroups.EmploymentSearch
                },
                Label = "Help to Seek Employment",
                Description =
                    "Browse vacancies for an unemployed adult relative living in this household " +
                    "and help them apply for a specific position.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor, actionContext.ActorHasControl)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || target.Age < 18
                        || target.Tags.Has("vocation.religious.active"))
                    {
                        return false;
                    }

                    var targetCareer = career.GetCareer(target);
                    if (targetCareer.IsRetired
                        || targetCareer.IsEmployed)
                    {
                        return false;
                    }

                    return IsResidentSupportedRelative(
                        actor,
                        target,
                        family,
                        economy);
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor, actionContext.ActorHasControl)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || target.Age < 18
                        || target.Tags.Has("vocation.religious.active"))
                    {
                        return new GameActionResult(false);
                    }

                    var targetCareer = career.GetCareer(target);
                    if (!IsResidentSupportedRelative(
                            actor,
                            target,
                            family,
                            economy)
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
                    var successChance = opportunity.SuccessChance;

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
                Presentation = new()
                {
                    Emoji = "🔎",
                    Categories = [ActionPresentationCategories.Career, ActionPresentationCategories.Family],
                    PlacementAnchor = ActionPresentationGroups.EmploymentSearch
                },
                Label = "Find a Better Job",
                Description =
                    "Browse better-paying vacancies for an employed adult relative living in this household.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor, actionContext.ActorHasControl)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || target.Age < 18
                        || target.Tags.Has("state.imprisoned")
                        || target.Tags.Has("vocation.religious.active"))
                    {
                        return false;
                    }

                    if (!IsResidentSupportedRelative(
                            actor,
                            target,
                            family,
                            economy))
                    {
                        return false;
                    }

                    var targetCareer = career.GetCareer(target);
                    return !targetCareer.IsRetired
                        && targetCareer.IsEmployed
                        && (targetCareer.IsSelfEmployed || targetCareer.JobLevel < 3);
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor, actionContext.ActorHasControl)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || target.Age < 18
                        || target.Tags.Has("vocation.religious.active"))
                    {
                        return new GameActionResult(false);
                    }

                    var targetCareer = career.GetCareer(target);

                    if (!IsResidentSupportedRelative(
                            actor,
                            target,
                            family,
                            economy)
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
                Presentation = new()
                {
                    Emoji = "🧘",
                    Categories = [ActionPresentationCategories.Personal, ActionPresentationCategories.Career, ActionPresentationCategories.Family, ActionPresentationCategories.Finances]
                },
                Label = "Ask to Recover",
                Description =
                    "Ask a working adult relative in this household to take the year easier. " +
                    "They may refuse. On success they recover Health and reduce their work output/income for the year.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor, actionContext.ActorHasControl)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id)
                    {
                        return false;
                    }

                    if (!IsRecoverOrQuitTarget(
                        actor,
                        target,
                        family,
                        economy))
                    {
                        return false;
                    }

                    return IsCurrentlyWorking(
                        actor,
                        target,
                        career,
                        craftResolver,
                        farmingResolver);
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor, actionContext.ActorHasControl)
                        || !target.Tags.Has("state.alive")
                        || !IsRecoverOrQuitTarget(
                            actor,
                            target,
                            family,
                            economy))
                    {
                        return new GameActionResult(false);
                    }

                    var targetCareer =
                        career.GetCareer(target);

                    if (!IsCurrentlyWorking(
                            actor,
                            target,
                            career,
                            craftResolver,
                            farmingResolver))
                    {
                        return new GameActionResult(false);
                    }

                    if (random.NextDouble() > 0.5)
                    {
                        if (targetCareer.IsEmployed)
                        {
                            career.ChangeJobSatisfaction(
                                target,
                                2);
                        }

                        target.Tags.Add(
                            "modifier.recover");

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
                Presentation = new()
                {
                    Emoji = "🚶",
                    Categories = [ActionPresentationCategories.Career, ActionPresentationCategories.Family]
                },
                Label = "Ask to Quit Job",
                Description =
                    "Ask an employed adult relative living in this household to quit so they can focus on the household, including farm work. " +
                    "There is a 50% refusal chance.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor, actionContext.ActorHasControl)
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || !IsRecoverOrQuitTarget(
                            actor,
                            target,
                            family,
                            economy))
                    {
                        return false;
                    }

                    if (target.Tags.Has("vocation.religious.active"))
                        return false;

                    var targetCareer =
                        career.GetCareer(target);

                    return targetCareer.IsEmployed;
                },

                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;
                    var target = actionContext.Target;

                    if (!CanActorSupport(actor, actionContext.ActorHasControl)
                        || !target.Tags.Has("state.alive")
                        || !IsRecoverOrQuitTarget(
                            actor,
                            target,
                            family,
                            economy))
                    {
                        return new GameActionResult(false);
                    }

                    if (target.Tags.Has("vocation.religious.active"))
                        return new GameActionResult(false);

                    var targetCareer =
                        career.GetCareer(target);

                    if (!targetCareer.IsEmployed)
                        return new GameActionResult(false);

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

    private static bool IsCurrentlyWorking(
        IPerson actor,
        IPerson target,
        StandardCareerService career,
        Func<ICraftService?> craftResolver,
        Func<IFarmingService?> farmingResolver)
    {
        if (career.GetCareer(target).IsEmployed)
            return true;

        if (craftResolver()?.IsSelfEmployed(target) == true)
            return true;

        return farmingResolver()?.IsWorkingFarmWorker(target, actor) == true;
    }
}
