using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

/// <summary>Shared spouse-search eligibility; queries current family state without caching it.</summary>
internal sealed class AutonomousReproductiveEligibility
{
    private readonly IFamilyService _family;

    public AutonomousReproductiveEligibility(
        IFamilyService family)
    {
        _family = family;
    }

    public bool CanSearchForReproductiveSpouse(IPerson head, int fertility = 1)
    {
        if (!CanParticipateInFamilyLife(head)
            || head.Age < 18
            || fertility <= 0
            || _family.GetSex(head) != Sex.Male
            || _family.GetSpouse(head) is { } spouse
                && spouse.Tags.Has("state.alive"))
        {
            return false;
        }

        return AutonomousStrategyRules.CanSearchForReproductiveFemale(
            head.Age,
            head.Tags.Has("morals.good"),
            head.Tags.Has("morals.evil"),
            head.Tags.Has("sexuality.homosexual"));
    }

    internal static bool CanParticipateInFamilyLife(IPerson person) =>
        person.Tags.Has("state.alive")
        && !person.Tags.Has("state.dead")
        && !person.Tags.Has("state.imprisoned")
        && !person.Tags.Has("vocation.religious.active")
        && !SimulationState.IsInactive(person)
        && !SimulationState.IsExternallyResident(person);
}
