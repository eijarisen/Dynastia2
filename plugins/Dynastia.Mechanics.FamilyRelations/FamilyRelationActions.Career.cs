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
        Label = "Use Family Connections",
        Description = "Ask this relative's household to use its career connections. If they agree, every eligible adult in the active household below job level 3 advances by one level.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is { } targetHead
            && HasDecentCareerConnection(targetHead, economy, career, gameState)
            && GetCareerHelpCandidates(c.Actor, economy, career, gameState).Count > 0,
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            if (targetHead is null
                || !HasDecentCareerConnection(targetHead, economy, career, gameState))
            {
                return new(false);
            }

            var candidates = GetCareerHelpCandidates(c.Actor, economy, career, gameState);
            if (candidates.Count == 0)
                return new(false);

            if (random.NextDouble() >= relations.EvaluateRequestWillingness(c.Actor, c.Target))
            {
                relations.ModifyRelation(c.Actor, c.Target, -5);
                Publish(
                    events,
                    c,
                    "family_relations.job_help_refused",
                    family,
                    $"{family.GetDisplayName(c.Target)} declined to use family connections for {family.GetDisplayName(c.Actor)}'s household.");
                return new(true);
            }

            var helped = ApplyCareerHelp(candidates, career);
            relations.ModifyRelation(c.Actor, c.Target, 5);
            Publish(
                events,
                c,
                "family_relations.job_help_received",
                family,
                $"{family.GetDisplayName(c.Target)} used family connections to advance the careers of {helped} adult household {(helped == 1 ? "member" : "members")}.");
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
        Label = "Help with Careers",
        Description = "Use the active household's career connections to help this relative's household. Every eligible adult below job level 3 advances by one level. No approval roll is needed.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is { } targetHead
            && HasDecentCareerConnection(c.Actor, economy, career, gameState)
            && GetCareerHelpCandidates(targetHead, economy, career, gameState).Count > 0,
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            if (targetHead is null
                || !HasDecentCareerConnection(c.Actor, economy, career, gameState))
            {
                return new(false);
            }

            var candidates = GetCareerHelpCandidates(targetHead, economy, career, gameState);
            if (candidates.Count == 0)
                return new(false);

            var helped = ApplyCareerHelp(candidates, career);
            relations.ModifyRelation(c.Actor, c.Target, 5);
            Publish(
                events,
                c,
                "family_relations.job_help_given",
                family,
                $"{family.GetDisplayName(c.Actor)} used family connections to advance the careers of {helped} adult household {(helped == 1 ? "member" : "members")} in {family.GetDisplayName(c.Target)}'s household.");
            return new(true);
        }
    };

    private static IReadOnlyList<IPerson> GetCareerHelpCandidates(
        IPerson householdRepresentative,
        IEconomyService economy,
        ICareerService career,
        IGameState gameState)
    {
        var ids = economy.GetHouseholdMemberIds(householdRepresentative).ToHashSet();
        return gameState.People
            .Where(p => ids.Contains(p.Id)
                && p.Tags.Has("state.alive")
                && p.Age >= 18
                && !p.Tags.Has("state.imprisoned")
                && !p.Tags.Has("role.nanny")
                && !p.Tags.Has("role.family_nanny"))
            .Where(p =>
            {
                var snapshot = career.GetCareer(p);
                return !snapshot.IsRetired && snapshot.JobLevel < 3;
            })
            .OrderByDescending(p => p.Id == householdRepresentative.Id)
            .ThenBy(p => p.Age)
            .ThenBy(p => p.Id)
            .ToList();
    }

    private static bool HasDecentCareerConnection(
        IPerson householdRepresentative,
        IEconomyService economy,
        ICareerService career,
        IGameState gameState)
    {
        var ids = economy.GetHouseholdMemberIds(householdRepresentative).ToHashSet();
        return gameState.People
            .Where(p => ids.Contains(p.Id)
                && p.Tags.Has("state.alive")
                && p.Age >= 18
                && !p.Tags.Has("state.imprisoned"))
            .Any(p =>
            {
                var snapshot = career.GetCareer(p);
                return !snapshot.IsRetired && snapshot.JobLevel >= 3;
            });
    }

    private static int ApplyCareerHelp(
        IReadOnlyList<IPerson> candidates,
        ICareerService career)
    {
        foreach (var person in candidates)
        {
            var current = career.GetCareer(person);
            career.SetJobLevel(person, Math.Min(3, current.JobLevel + 1));
        }

        return candidates.Count;
    }
}
