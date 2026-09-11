using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal sealed class DivorcedParentsHealthModifierProvider :
    IAnnualHealthModifierProvider
{
    private const double AnnualPenalty =
        1;

    public string Id =>
        "relationships.parents_divorced_health";

    public double GetAnnualHealthChange(
        IPerson person)
    {
        return person.Age < 18
            && person.Tags.Has(
                "state.alive")
            && person.Tags.Has(
                DivorcedParentsTracker.Tag)
                    ? -AnnualPenalty
                    : 0;
    }
}
