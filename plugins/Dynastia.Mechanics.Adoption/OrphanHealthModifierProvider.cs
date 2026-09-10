using Dynastia.Contracts;

namespace Dynastia.Mechanics.Adoption;

public sealed class OrphanHealthModifierProvider :
    IAnnualHealthModifierProvider
{
    private const double OrphanPenalty =
        -1;

    public string Id =>
        "adoption.orphan_trait";

    public double GetAnnualHealthChange(
        IPerson person)
    {
        return person.Tags.Has(
            "trait.orphan")
                ? OrphanPenalty
                : 0;
    }
}
