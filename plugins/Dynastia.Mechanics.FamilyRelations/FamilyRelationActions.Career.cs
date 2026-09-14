using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal static partial class FamilyRelationActions
{
    private static GameActionDefinition CreateAskJobHelp(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        ICareerService career,
        IGameRandom random,
        IGameEventBus events,
        IFamilyService family,
        IGameState gameState) => new()
    {
        Id = "family_relations.ask_job_help",
        Label = "Ask for Job Help",
        Description = "Ask this autonomous relative to use a useful career connection. Agreement grants a normal local job attempt with a family-connection bonus.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && TryGetAutonomousTargetHead(c.Target, households, out var targetHead)
            && targetHead is not null
            && FindUnemployedAdult(c.Actor, economy, career, gameState) is not null
            && HasUsefulConnection(targetHead, economy, career, gameState),
        Execute = c =>
        {
            if (!TryGetAutonomousTargetHead(c.Target, households, out var targetHead) || targetHead is null)
                return new(false);
            var worker = FindUnemployedAdult(c.Actor, economy, career, gameState);
            if (worker is null || !HasUsefulConnection(targetHead, economy, career, gameState))
                return new(false);

            if (random.NextDouble() >= relations.EvaluateRequestWillingness(c.Actor, c.Target))
            {
                relations.ModifyRelation(c.Actor, c.Target, -5);
                Publish(events, c, "family_relations.job_help_refused", family,
                    $"{family.GetDisplayName(c.Target)} declined to help {family.GetDisplayName(worker)} find work.");
                return new(true);
            }

            var found = career.TryFindEmployment(worker, JobConnectionBonus);
            relations.ModifyRelation(c.Actor, c.Target, found ? 5 : 2);
            Publish(events, c, "family_relations.job_help_received", family,
                found
                    ? $"{family.GetDisplayName(c.Target)} used family connections to help {family.GetDisplayName(worker)} find work."
                    : $"{family.GetDisplayName(c.Target)} tried to help {family.GetDisplayName(worker)} find work, but no suitable position was secured.");
            return new(true);
        }
    };

    private static GameActionDefinition CreateGiveJobHelp(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        ICareerService career,
        IGameEventBus events,
        IFamilyService family,
        IGameState gameState) => new()
    {
        Id = "family_relations.give_job_help",
        Label = "Give Job Help",
        Description = "Use this household's career connections to help an unemployed adult relative. No approval roll is needed; the local labour market still decides whether a job is found.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is { } targetHead
            && FindUnemployedAdult(targetHead, economy, career, gameState) is not null
            && HasUsefulConnection(c.Actor, economy, career, gameState),
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            if (targetHead is null || !HasUsefulConnection(c.Actor, economy, career, gameState))
                return new(false);
            var worker = FindUnemployedAdult(targetHead, economy, career, gameState);
            if (worker is null) return new(false);
            var found = career.TryFindEmployment(worker, JobConnectionBonus);
            relations.ModifyRelation(c.Actor, c.Target, found ? 5 : 2);
            Publish(events, c, "family_relations.job_help_given", family,
                found
                    ? $"{family.GetDisplayName(c.Actor)} used family connections to help {family.GetDisplayName(worker)} find work."
                    : $"{family.GetDisplayName(c.Actor)} tried to help {family.GetDisplayName(worker)} find work, but no suitable position was secured.");
            return new(true);
        }
    };

    private static IPerson? FindUnemployedAdult(
        IPerson householdRepresentative,
        IEconomyService economy,
        ICareerService career,
        IGameState gameState)
    {
        var ids = economy.GetHouseholdMemberIds(householdRepresentative).ToHashSet();
        return gameState.People
            .Where(p => ids.Contains(p.Id) && p.Tags.Has("state.alive") && p.Age >= 18 && !p.Tags.Has("state.imprisoned"))
            .Where(p => { var c = career.GetCareer(p); return !c.IsRetired && c.JobLevel == 0; })
            .OrderByDescending(p => p.Id == householdRepresentative.Id)
            .ThenBy(p => p.Age)
            .FirstOrDefault();
    }

    private static bool HasUsefulConnection(
        IPerson householdRepresentative,
        IEconomyService economy,
        ICareerService career,
        IGameState gameState)
    {
        var ids = economy.GetHouseholdMemberIds(householdRepresentative).ToHashSet();
        return gameState.People
            .Where(p => ids.Contains(p.Id) && p.Tags.Has("state.alive") && p.Age >= 18)
            .Any(p => { var c = career.GetCareer(p); return !c.IsRetired && c.JobLevel >= 2; });
    }

}
