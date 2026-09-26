using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed partial class StandardHouseholdService :
    IHouseholdService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IHouseholdCapacityService _capacity;
    private readonly ILocationService _locations;
    private readonly ICareerService _career;
    private readonly IFarmingService _farming;
    private readonly IGameEventBus _events;

    private bool _reconciling;

    public StandardHouseholdService(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        IHouseholdCapacityService capacity,
        ILocationService locations,
        ICareerService career,
        IFarmingService farming,
        IGameEventBus events)
    {
        _gameState =
            gameState;

        _family =
            family;

        _economy =
            economy;

        _capacity =
            capacity;

        _locations =
            locations;

        _career =
            career;

        _farming =
            farming;

        _events =
            events;
    }

    public IPerson? ResolveHouseholdHead(
        IPerson person)
    {
        var householdId =
            _economy.GetHouseholdId(
                person);

        if (householdId is null)
            return null;

        return _gameState.People
            .FirstOrDefault(
                candidate =>
                    _economy.HasHousehold(
                        candidate)
                    && _economy.GetHouseholdId(
                        candidate)
                        == householdId);
    }

    public HouseholdStatusSnapshot? GetStatus(
        IPerson head)
    {
        var actualHead =
            ResolveHouseholdHead(
                head)
            ?? (
                _economy.HasHousehold(head)
                    ? head
                    : null
            );

        if (actualHead is null)
            return null;

        var finance =
            _economy.GetHousehold(
                actualHead);

        if (finance is null)
            return null;

        var members =
            GetLivingMembers(
                actualHead);

        var underageChildren =
            members.Count(
                member =>
                    member.Age < 18
                    && !member.Tags.Has(
                        "role.nanny"));

        var residentCount =
            members.Count(
                member =>
                    !member.Tags.Has(
                        "role.nanny"));

        var overcrowdingThreshold =
            _capacity.GetResidenceCapacity(actualHead).ResidentCapacity;
        var overcrowded =
            HouseholdCrowdingRules.IsOvercrowded(
                residentCount,
                overcrowdingThreshold);


        var spouse =
            _family.GetSpouse(
                actualHead);

        if (spouse is not null
            && !spouse.Tags.Has(
                "state.alive"))
        {
            spouse =
                null;
        }

        var isHousewife =
            spouse is not null
            && _career.GetCareer(
                    spouse)
                .StatusId?.Equals(
                    "status.housewife",
                    StringComparison.OrdinalIgnoreCase) == true;

        var baseCapacity =
            isHousewife
                ? 4
                : 3;

        var hasNannyReference =
            finance.NannyId.HasValue;

        var effectiveCapacity =
            baseCapacity
            + (
                hasNannyReference
                    ? 2
                    : 0
            );

        var strained =
            underageChildren
                > effectiveCapacity;

        var atCapacity =
            actualHead.Tags.Has(
                "state.alive")
            && underageChildren
                == effectiveCapacity;

        var broke =
            finance.Wealth <= 0;

        var hasUnfundedBasicNeeds =
            finance.HasUnfundedBasicNeeds;

        var nanny =
            GetNanny(
                actualHead);

        var warnings =
            new List<string>();

        if (strained)
        {
            var excessChildren =
                underageChildren - effectiveCapacity;

            warnings.Add(
                $"The household has {excessChildren} child{(excessChildren == 1 ? "" : "ren")} above its supported family capacity, gradually increasing Health loss and Stress.");
        }
        else if (atCapacity)
        {
            warnings.Add(
                "Having more kids will strain the family.");
        }

        if (hasUnfundedBasicNeeds)
        {
            warnings.Add(
                "The household could not fully cover its basic needs this year.");
        }

        if (overcrowded)
        {
            var excessResidents =
                HouseholdCrowdingRules.GetResidentsAboveCapacity(
                    residentCount,
                    overcrowdingThreshold);

            warnings.Add(
                $"The household is overcrowded by {excessResidents} resident{(excessResidents == 1 ? "" : "s")}; each additional resident gradually increases Stress and Health loss.");
        }
        else if (overcrowdingThreshold > 0
                 && residentCount == overcrowdingThreshold)
        {
            warnings.Add(
                "The house is at its resident capacity; another resident will cause overcrowding.");
        }

        return new HouseholdStatusSnapshot(
            actualHead.Id,
            underageChildren,
            baseCapacity,
            effectiveCapacity,
            finance.NannyId,
            nanny is null
                ? null
                : _family.GetDisplayName(
                    nanny),
            hasNannyReference,
            strained,
            atCapacity,
            broke,
            hasUnfundedBasicNeeds,
            warnings,
            _career.GetStatusLabel(
                "role.nanny"),
            residentCount,
            overcrowdingThreshold,
            overcrowded);
    }

    public IPerson? GetNanny(
        IPerson head)
    {
        var finance =
            _economy.GetHousehold(
                head);

        if (finance?.NannyId
            is not Guid id)
        {
            return null;
        }

        return _gameState.People
            .FirstOrDefault(
                person =>
                    person.Id
                    == id);
    }

    public IReadOnlyList<HouseholdInfo>
        GetActiveHouseholds()
    {
        var result =
            new List<HouseholdInfo>();

        foreach (var head in
            _gameState.People
                .Where(
                    person =>
                        _economy.HasHousehold(
                            person))
                .ToList())
        {
            if (SimulationState.IsExternallyResident(head))
                continue;

            if (_economy.IsEstateReady(
                head))
            {
                continue;
            }

            var memberIds =
                _economy
                    .GetHouseholdMemberIds(
                        head)
                    .Where(
                        id =>
                            _gameState.People.Any(
                                person =>
                                    person.Id == id
                                    && person.Tags.Has(
                                        "state.alive")))
                    .ToList();

            var hasLivingBloodline =
                memberIds
                    .Select(
                        FindPerson)
                    .Where(
                        person =>
                            person is not null)
                    .Cast<IPerson>()
                    .Any(
                        person =>
                            _family.IsBloodline(
                                person));

            if (!hasLivingBloodline)
                continue;

            var anchorId =
                _economy
                    .GetHouseholdDynastyAnchorId(
                        head)
                ?? head.Id;

            var anchor =
                FindPerson(
                    anchorId)
                ?? head;

            var householdClass =
                head.Tags.Has(
                    "state.alive")
                && head.Age >= 18
                && _family.GetSex(
                    head) == Sex.Male
                && _family.IsMaleLineage(
                    head)
                    ? HouseholdClass.Lineage
                    : HouseholdClass.Bloodline;

            result.Add(
                new HouseholdInfo(
                    _economy.GetHouseholdId(
                        head)
                    ?? Guid.Empty,
                    head.Id,
                    anchor.Id,
                    _family.GetGeneration(
                        anchor),
                    _family.FormatSurname(
                        head,
                        head.Surname,
                        _family.GetSex(
                            head)),
                    _family.GetDisplayName(
                        head),
                    householdClass,
                    memberIds));
        }

        return result
            .OrderBy(
                household =>
                    household.Generation
                    ?? int.MaxValue)
            .ThenBy(
                household =>
                    household.Surname,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                household =>
                    household.HeadName,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public HouseholdInfo? GetHouseholdInfo(
        IPerson person)
    {
        var householdId =
            _economy.GetHouseholdId(
                person);

        if (householdId is null)
            return null;

        var head =
            ResolveHouseholdHead(
                person);

        if (head is null
            || _economy.IsEstateReady(
                head))
        {
            return null;
        }

        var anchorId =
            _economy
                .GetHouseholdDynastyAnchorId(
                    head)
            ?? head.Id;

        var anchor =
            FindPerson(
                anchorId)
            ?? head;

        var householdClass =
            head.Tags.Has(
                "state.alive")
            && head.Age >= 18
            && _family.GetSex(
                head) == Sex.Male
            && _family.IsMaleLineage(
                head)
                ? HouseholdClass.Lineage
                : HouseholdClass.Bloodline;

        return new HouseholdInfo(
            householdId.Value,
            head.Id,
            anchor.Id,
            _family.GetGeneration(
                anchor),
            _family.FormatSurname(
                head,
                head.Surname,
                _family.GetSex(
                    head)),
            _family.GetDisplayName(
                head),
            householdClass,
            _economy.GetHouseholdMemberIds(
                head));
    }

    public bool IsAutonomousHousehold(
        IPerson person)
    {
        return GetHouseholdInfo(
            person)?.Class
            == HouseholdClass.Bloodline;
    }

    public void ReconcileHouseholds()
    {
        if (_reconciling)
            return;

        _reconciling =
            true;

        try
        {
            CollapseInvalidMinorHeadHouseholds();

            DissolveEmptyDeadHouseholdTombstones();

            SeedLegacyHouseholdMemberships();

            ReconcileRemarriedCaregiverHouseholds();

            ReconcileMarriedBloodlineWomen();

            ReconcileCurrentSpouses();

            ReconcileBloodlineDependents();

            EnsureAdultBloodlineHouseholds();

            AttachSurvivingUnmarriedParentsToDeadHeadHouseholds();

            CleanupFormerPartners();

            ReconcileHeadSuccession();

            TagPeripheralFormerPartners();
        }
        finally
        {
            _reconciling =
                false;
        }
    }

}
