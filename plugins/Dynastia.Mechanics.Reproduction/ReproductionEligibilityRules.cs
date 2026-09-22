using Dynastia.Contracts;

namespace Dynastia.Mechanics.Reproduction;

internal static class ReproductionEligibilityRules
{
    internal const int MinimumChildbearingAge = 18;
    internal const int MaximumChildbearingAge = 45;

    public static bool CanAttemptMaritalConception(
        IFamilyService family,
        IEconomyService economy,
        IPerson father,
        IPerson? mother,
        int year)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(father);

        if (mother is null
            || !father.Tags.Has("state.alive")
            || !mother.Tags.Has("state.alive")
            || family.GetSex(father) != Sex.Male
            || family.GetSex(mother) != Sex.Female)
        {
            return false;
        }

        if (father.Tags.Has("state.imprisoned")
            || mother.Tags.Has("state.imprisoned")
            || father.Tags.Has("vocation.religious.active")
            || mother.Tags.Has("vocation.religious.active")
            || SimulationState.IsInactive(father)
            || SimulationState.IsInactive(mother)
            || SimulationState.IsExternallyResident(father)
            || SimulationState.IsExternallyResident(mother))
        {
            return false;
        }

        if (mother.Age < MinimumChildbearingAge
            || mother.Age > MaximumChildbearingAge)
        {
            return false;
        }

        if (family.GetSpouse(father)?.Id != mother.Id
            || family.GetSpouse(mother)?.Id != father.Id)
        {
            return false;
        }

        var activeMarriage = family.GetRelationshipHistory(father)
            .LastOrDefault(relationship =>
                relationship.SpouseId == mother.Id
                && relationship.EndYear is null);

        if (activeMarriage is null
            || activeMarriage.StartYear == year)
        {
            return false;
        }

        var fatherHouseholdId = economy.GetHouseholdId(father);
        var motherHouseholdId = economy.GetHouseholdId(mother);

        return fatherHouseholdId is Guid householdId
            && motherHouseholdId == householdId;
    }
}
