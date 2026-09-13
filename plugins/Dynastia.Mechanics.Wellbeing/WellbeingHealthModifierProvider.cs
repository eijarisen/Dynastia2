using Dynastia.Contracts;

namespace Dynastia.Mechanics.Wellbeing;

public sealed class WellbeingHealthModifierProvider :
    IAnnualHealthModifierProvider
{
    private const double RecoverHealthBonus =
        15;

    public string Id =>
        "wellbeing.recover";

    public double GetAnnualHealthChange(
        IPerson person)
    {
        return person.Tags.Has(
            "modifier.recover")
                ? RecoverHealthBonus
                : 0;
    }
}
