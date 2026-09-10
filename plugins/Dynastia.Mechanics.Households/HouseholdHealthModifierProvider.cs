using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed class HouseholdHealthModifierProvider :
    IAnnualHealthModifierProvider
{
    private const double LargeFamilyPenalty = 4;

    private const double BrokeBase = 5;
    private const double BrokeModifier = 10;

    private const double OrphanBase = 3;
    private const double OrphanModifier = 6;

    private readonly IHouseholdService _households;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;

    public HouseholdHealthModifierProvider(
        IHouseholdService households,
        IFamilyService family,
        IStatsService stats)
    {
        _households = households;
        _family = family;
        _stats = stats;
    }

    public string Id =>
        "households.health_modifiers";

    public double GetAnnualHealthChange(
        IPerson person)
    {
        var isOrphan =
            person.Age < 18
            && !IsAlive(
                _family.GetFather(person))
            && !IsAlive(
                _family.GetMother(person));

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
                    var spouse =
                        _family.GetSpouse(head);

                    var penalty =
                        person.Id == spouse?.Id
                            ? LargeFamilyPenalty * 2
                            : LargeFamilyPenalty;

                    change -= penalty;
                }

                if (status.IsBroke)
                {
                    var immunity =
                        GetImmunity(person);

                    change -=
                        BrokeBase
                        + (BrokeModifier
                           - immunity * 2);
                }
            }

            return change;
        }

        if (isOrphan)
        {
            var immunity =
                GetImmunity(person);

            return -(
                OrphanBase
                + (OrphanModifier - immunity));
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

    private static bool IsAlive(
        IPerson? person)
    {
        return person is not null
            && person.Tags.Has("state.alive");
    }
}
