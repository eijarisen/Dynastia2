using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed class HouseholdHealthModifierProvider :
    IAnnualHealthModifierProvider,
    IPreparedAnnualHealthModifierProvider
{
    private const double LargeFamilyHealthPenaltyPerExcessChild = 1.5;
    private const double LargeFamilySpouseMultiplier = 1.5;

    private const double BrokeBase = 5;
    private const double BrokeModifier = 10;

    private readonly IHouseholdService _households;
    private readonly IEconomyService? _economy;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly ICareerService _career;

    private readonly Dictionary<Guid, PreparedPersonContext>
        _preparedPeople = [];

    private bool _prepared;

    public HouseholdHealthModifierProvider(
        IHouseholdService households,
        IFamilyService family,
        IStatsService stats,
        ICareerService career)
        : this(
            households,
            null,
            family,
            stats,
            career)
    {
    }

    public HouseholdHealthModifierProvider(
        IHouseholdService households,
        IEconomyService? economy,
        IFamilyService family,
        IStatsService stats,
        ICareerService career)
    {
        _households = households;
        _economy = economy;
        _family = family;
        _stats = stats;
        _career = career;
    }

    public string Id =>
        "households.health_modifiers";

    public void PrepareAnnualHealthContext(
        IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(
            gameState);

        _preparedPeople.Clear();

        var economy =
            _economy;

        if (economy is null)
        {
            _prepared = false;
            return;
        }

        _prepared = true;

        var peopleById =
            new Dictionary<Guid, IPerson>();

        var headsByHousehold =
            new Dictionary<Guid, IPerson>();

        var householdOrder =
            new List<Guid>();

        var personHouseholds =
            new Dictionary<Guid, Guid>();

        // Discover household heads in authoritative People order. For a head,
        // GetHouseholdId is a direct component lookup because HasHousehold has
        // already confirmed ownership.
        foreach (var person in
            gameState.People)
        {
            peopleById.TryAdd(
                person.Id,
                person);

            if (!economy.HasHousehold(person))
                continue;

            if (economy.GetHouseholdId(person)
                is not Guid householdId)
            {
                continue;
            }

            if (!headsByHousehold.TryAdd(
                    householdId,
                    person))
            {
                continue;
            }

            householdOrder.Add(
                householdId);

            // A direct household owner wins before membership scans, matching
            // StandardEconomyService.FindHousehold's direct-component path.
            personHouseholds[person.Id] =
                householdId;
        }

        // Expand membership from each head in the same order ResolveHouseholdHead
        // would discover households. TryAdd preserves first-match semantics.
        foreach (var householdId in
            householdOrder)
        {
            var head =
                headsByHousehold[householdId];

            foreach (var memberId in
                economy.GetHouseholdMemberIds(head))
            {
                personHouseholds.TryAdd(
                    memberId,
                    householdId);
            }
        }

        var relevantHouseholds =
            new HashSet<Guid>();

        foreach (var membership in
            personHouseholds)
        {
            if (peopleById.TryGetValue(
                    membership.Key,
                    out var person)
                && !person.Tags.Has(
                    "state.dead")
                && !SimulationState.IsInactive(
                    person))
            {
                relevantHouseholds.Add(
                    membership.Value);
            }
        }

        var households =
            new Dictionary<Guid, PreparedHouseholdContext>();

        foreach (var householdId in
            householdOrder)
        {
            if (!relevantHouseholds.Contains(
                    householdId))
            {
                continue;
            }

            var head =
                headsByHousehold[householdId];

            var status =
                _households.GetStatus(
                    head);

            var spouse =
                _family.GetSpouse(
                    head);

            var hasRetiredWife =
                spouse is not null
                && spouse.Tags.Has(
                    "state.alive")
                && _family.GetSex(
                    spouse) == Sex.Female
                && _career.GetCareer(
                    spouse)
                    .IsRetired;

            households.Add(
                householdId,
                new PreparedHouseholdContext(
                    head,
                    status,
                    spouse?.Id,
                    hasRetiredWife));
        }

        foreach (var membership in
            personHouseholds)
        {
            peopleById.TryGetValue(
                membership.Key,
                out var person);

            if (person is null
                || !households.TryGetValue(
                    membership.Value,
                    out var household))
            {
                continue;
            }

            var eligible =
                household.Head.Tags.Has(
                    "state.alive")
                || _family.GetSpouse(
                    person)?.Id
                    == household.Head.Id;

            _preparedPeople[person.Id] =
                eligible
                    ? new PreparedPersonContext(
                        household.Status,
                        household.SpouseId,
                        household.HasRetiredWife)
                    : PreparedPersonContext.None;
        }
    }

    public void ClearAnnualHealthContext()
    {
        _preparedPeople.Clear();
        _prepared = false;
    }

    public double GetAnnualHealthChange(
        IPerson person)
    {
        if (_prepared)
        {
            return _preparedPeople.TryGetValue(
                    person.Id,
                    out var prepared)
                ? GetPreparedAnnualHealthChange(
                    person,
                    prepared)
                : 0;
        }

        return GetLiveAnnualHealthChange(
            person);
    }

    private double GetPreparedAnnualHealthChange(
        IPerson person,
        PreparedPersonContext prepared)
    {
        if (!prepared.IsEligible)
            return 0;

        var change =
            0.0;

        var status =
            prepared.Status;

        if (status is not null)
        {
            if (status.IsLargeFamilyStrained)
            {
                var excessChildren = Math.Max(
                    0,
                    status.UnderageChildren - status.EffectiveChildCapacity);

                var penalty =
                    excessChildren
                    * LargeFamilyHealthPenaltyPerExcessChild;

                if (person.Id == prepared.SpouseId)
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

                if (person.Age < 18)
                {
                    povertyPenalty *= 0.50;
                }

                change -= povertyPenalty;
            }
        }

        if (prepared.HasRetiredWife)
        {
            change +=
                1;
        }

        return change;
    }

    private double GetLiveAnnualHealthChange(
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

    private sealed record PreparedHouseholdContext(
        IPerson Head,
        HouseholdStatusSnapshot? Status,
        Guid? SpouseId,
        bool HasRetiredWife);

    private sealed record PreparedPersonContext(
        bool IsEligible,
        HouseholdStatusSnapshot? Status,
        Guid? SpouseId,
        bool HasRetiredWife)
    {
        public PreparedPersonContext(
            HouseholdStatusSnapshot? status,
            Guid? spouseId,
            bool hasRetiredWife)
            : this(
                true,
                status,
                spouseId,
                hasRetiredWife)
        {
        }

        public static PreparedPersonContext None { get; } =
            new(
                false,
                null,
                null,
                false);
    }
}
