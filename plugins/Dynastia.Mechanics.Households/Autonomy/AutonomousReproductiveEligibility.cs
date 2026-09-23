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

    public bool CanSearchForReproductiveSpouse(IPerson head)
    {
        if (_family.GetSex(head) != Sex.Male
            || _family.GetSpouse(head) is not null)
        {
            return false;
        }

        return AutonomousStrategyRules.CanSearchForReproductiveFemale(
            head.Age,
            head.Tags.Has("morals.good"),
            head.Tags.Has("morals.evil"),
            head.Tags.Has("sexuality.homosexual"));
    }
}
