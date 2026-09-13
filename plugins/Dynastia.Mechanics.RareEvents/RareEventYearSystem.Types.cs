using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed partial class RareEventYearSystem
{
    private enum HouseholdRareEvent
    {
        HouseFire,
        Burglary,
        StormOrFlood,
        StructuralAccident
    }

    private enum PersonalRareEvent
    {
        Assault,
        Mugging,
        WorkplaceAccident,
        TrafficAccident,
        LightningStrike,
        SeriousFall,
        LotteryWin,
        DistantInheritance,
        Fraud,
        FoundProperty,
        WrongfulArrest,
        Suicide
    }

    private sealed record HouseholdEventCandidate(
        HouseholdRareEvent Event,
        double Chance);

    private sealed record PersonalEventCandidate(
        PersonalRareEvent Event,
        double Chance);

    private sealed record HouseholdContext(
        IPerson Head,
        IReadOnlyList<IPerson> Occupants,
        IPerson PrimaryOccupant);

    private sealed class HouseholdContextBuilder
    {
        public HouseholdContextBuilder(
            IPerson head)
        {
            Head =
                head;
        }

        public IPerson Head { get; }

        public List<IPerson> Occupants { get; } =
            [];
    }
}
