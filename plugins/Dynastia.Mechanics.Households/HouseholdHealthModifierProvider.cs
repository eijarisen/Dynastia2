using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed class HouseholdHealthModifierProvider :
    IAnnualHealthModifierProvider
{
    private const double LargeFamilyHealthPenaltyPerExcessChild = 1.5;
    private const double LargeFamilySpouseMultiplier = 1.5;

    private const double BrokeBase = 5;
    private const double BrokeModifier = 10;

    private readonly IHouseholdService _households;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly ICareerService _career;

    public HouseholdHealthModifierProvider(
        IHouseholdService households,
        IFamilyService family,
        IStatsService stats,
        ICareerService career)
    {
        _households = households;
        _family = family;
        _stats = stats;
        _career = career;
    }

    public string Id =>
        "households.health_modifiers";

    public double GetAnnualHealthChange(
        IPerson person)
    {
        var head =
            _households.ResolveHouseholdHead(
                person);

        if (head is not null
            && (head.Tags.Has("state.alive")
                || _family.GetSpouse(person)?.Id == head.Id))
        {
            var change =
                0.0;

            var status =
                _households.GetStatus(head);

            if (status is not null)
            {
                if (status.IsLargeFamilyStrained)
                {
                    var excessChildren = Math.Max(
                        0,
                        status.UnderageChildren - status.EffectiveChildCapacity);

                    var spouse =
                        _family.GetSpouse(head);

                    var penalty =
                        excessChildren
                        * LargeFamilyHealthPenaltyPerExcessChild;

                    if (person.Id == spouse?.Id)
                    {
                        penalty *= LargeFamilySpouseMultiplier;
                    }

                    change -= penalty;
                }

                if (status.IsOvercrowded)
                {
                    change -= HouseholdCrowdingRules.GetAnnualHealthPenalty(
                        status.ResidentCount,
                        status.OvercrowdingThreshold);
                }

                if (status.HasUnfundedBasicNeeds)
                {
                    var immunity =
                        GetImmunity(person);

                    var povertyPenalty =
                        BrokeBase
                        + (BrokeModifier
                           - immunity * 2);

                    // Poverty still matters for children, but it should not
                    // routinely become a direct death spiral while a surviving
                    // parent is trying to rebuild the household.
                    if (person.Age < 18)
                    {
                        povertyPenalty *= 0.50;
                    }

                    change -= povertyPenalty;
                }
            }

            var retiredWife =
                _family.GetSpouse(
                    head);

            if (retiredWife is not null
                && retiredWife.Tags.Has(
                    "state.alive")
                && _family.GetSex(
                    retiredWife)
                    == Sex.Female
                && _career.GetCareer(
                    retiredWife)
                    .IsRetired)
            {
                change +=
                    1;
            }

            return change;
        }

        return 0;
    }

    private int GetImmunity(
        IPerson person)
    {
        return _stats.GetStats(person)
            .First(stat =>
                stat.Id.Equals(
                    "immunity",
                    StringComparison.OrdinalIgnoreCase))
            .Value;
    }

}
