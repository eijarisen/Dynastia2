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


    private static string GetEventId(
        HouseholdRareEvent rareEvent) =>
        rareEvent switch
        {
            HouseholdRareEvent.HouseFire => "rare.house_fire",
            HouseholdRareEvent.Burglary => "rare.burglary",
            HouseholdRareEvent.StormOrFlood => "rare.storm_flood",
            HouseholdRareEvent.StructuralAccident => "rare.structural_accident",
            _ => throw new ArgumentOutOfRangeException(nameof(rareEvent))
        };

    private static string GetEventId(
        PersonalRareEvent rareEvent) =>
        rareEvent switch
        {
            PersonalRareEvent.Assault => "rare.assault",
            PersonalRareEvent.Mugging => "rare.mugging",
            PersonalRareEvent.WorkplaceAccident => "rare.workplace_accident",
            PersonalRareEvent.TrafficAccident => "rare.traffic_accident",
            PersonalRareEvent.LightningStrike => "rare.lightning_strike",
            PersonalRareEvent.SeriousFall => "rare.serious_fall",
            PersonalRareEvent.LotteryWin => "rare.lottery_win",
            PersonalRareEvent.DistantInheritance => "rare.distant_inheritance",
            PersonalRareEvent.Fraud => "rare.fraud",
            PersonalRareEvent.FoundProperty => "rare.found_property",
            PersonalRareEvent.WrongfulArrest => "rare.wrongful_arrest",
            PersonalRareEvent.Suicide => "rare.suicide",
            _ => throw new ArgumentOutOfRangeException(nameof(rareEvent))
        };

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
