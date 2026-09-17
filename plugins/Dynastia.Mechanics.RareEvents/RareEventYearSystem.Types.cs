using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed partial class RareEventYearSystem
{
    private sealed record EventCandidate(
        RareEventDefinition Definition,
        double Weight);

    private sealed record HouseholdContext(
        IPerson Head,
        IReadOnlyList<IPerson> Occupants,
        IPerson PrimaryOccupant);

    private sealed class HouseholdContextBuilder
    {
        public HouseholdContextBuilder(IPerson head) => Head = head;
        public IPerson Head { get; }
        public List<IPerson> Occupants { get; } = [];
    }
}
