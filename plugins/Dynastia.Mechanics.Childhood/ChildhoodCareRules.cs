using Dynastia.Contracts;

namespace Dynastia.Mechanics.Childhood;

internal static class ChildhoodCareRules
{
    public static bool HasAvailableResidentCaregiver(
        IPerson child,
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        IHouseholdService households)
    {
        var head = households.ResolveHouseholdHead(child);
        var householdId = economy.GetHouseholdId(child);
        if (head is null || householdId is null)
            return false;

        var memberIds = economy.GetHouseholdMemberIds(head).ToHashSet();
        memberIds.Add(head.Id);

        var nanny = households.GetNanny(head);
        if (nanny is not null)
            memberIds.Add(nanny.Id);

        return gameState.People.Any(candidate =>
            memberIds.Contains(candidate.Id)
            && IsAvailableCaregiverCandidate(child, candidate, family, economy));
    }

    public static bool WasResidentCaregiver(
        IPerson child,
        IPerson candidate,
        IFamilyService family,
        IEconomyService economy,
        IHouseholdService households)
    {
        if (candidate.Age < 18
            || SimulationState.IsInactive(candidate)
            || SimulationState.IsExternallyResident(candidate)
            || candidate.Tags.Has("state.imprisoned"))
        {
            return false;
        }

        var childHouseholdId = economy.GetHouseholdId(child);
        var candidateHouseholdId = economy.GetHouseholdId(candidate);
        if (childHouseholdId is Guid childHousehold
            && candidateHouseholdId == childHousehold)
        {
            return IsCaregiverRoleOrRelative(child, candidate, family);
        }

        var head = households.ResolveHouseholdHead(child);
        return head is not null
            && households.GetNanny(head)?.Id == candidate.Id;
    }

    public static bool SharesHousehold(
        IPerson first,
        IPerson second,
        IEconomyService economy)
    {
        var householdId = economy.GetHouseholdId(first);
        return householdId is not null
            && economy.GetHouseholdId(second) == householdId;
    }

    private static bool IsAvailableCaregiverCandidate(
        IPerson child,
        IPerson candidate,
        IFamilyService family,
        IEconomyService economy)
    {
        if (candidate.Id == child.Id
            || candidate.Age < 18
            || !candidate.Tags.Has("state.alive")
            || candidate.Tags.Has("state.imprisoned")
            || SimulationState.IsInactive(candidate)
            || SimulationState.IsExternallyResident(candidate))
        {
            return false;
        }

        if (candidate.Tags.Has("role.nanny")
            || candidate.Tags.Has("role.family_nanny"))
        {
            return true;
        }

        return HouseholdKinshipRules.IsSupportedResidentRelative(
            child,
            candidate,
            family,
            economy,
            requireAdult: true);
    }

    private static bool IsCaregiverRoleOrRelative(
        IPerson child,
        IPerson candidate,
        IFamilyService family) =>
        candidate.Tags.Has("role.nanny")
        || candidate.Tags.Has("role.family_nanny")
        || HouseholdKinshipRules.IsSupportedRelative(child, candidate, family);
}
