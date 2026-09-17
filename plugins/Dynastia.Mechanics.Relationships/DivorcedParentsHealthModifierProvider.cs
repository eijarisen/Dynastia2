using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

// Retained as a compatibility type for old builds/tests. Parental divorce is
// now represented through Stress rather than direct annual Health damage.
internal sealed class DivorcedParentsHealthModifierProvider :
    IAnnualHealthModifierProvider
{
    public string Id =>
        "relationships.parents_divorced_health";

    public double GetAnnualHealthChange(
        IPerson person) => 0;
}
