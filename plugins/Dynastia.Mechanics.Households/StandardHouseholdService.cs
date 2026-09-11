using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed class StandardHouseholdService :
    IHouseholdService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly ICareerService _career;
    private readonly IGameEventBus _events;

    private bool _reconciling;

    public StandardHouseholdService(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        ICareerService career,
        IGameEventBus events)
    {
        _gameState =
            gameState;

        _family =
            family;

        _economy =
            economy;

        _career =
            career;

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
                .JobTitle.Equals(
                    "Housewife",
                    StringComparison.OrdinalIgnoreCase);

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
                > effectiveCapacity
            && !hasNannyReference;

        var atCapacity =
            actualHead.Tags.Has(
                "state.alive")
            && underageChildren
                == effectiveCapacity
            && !hasNannyReference;

        var broke =
            finance.Wealth <= 0;

        var nanny =
            GetNanny(
                actualHead);

        var warnings =
            new List<string>();

        if (strained)
        {
            warnings.Add(
                "The large family size is putting a strain on everyone.");
        }
        else if (atCapacity)
        {
            warnings.Add(
                "Having more kids will strain the family.");
        }

        if (broke)
        {
            warnings.Add(
                "Being broke is negatively impacting the family's health.");
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
            warnings);
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

    public bool ShouldShowFamilyNews(
        GameEvent gameEvent)
    {
        var eventIndex =
            IndexOfEvent(
                gameEvent);

        var ledger =
            GetNewsLedger(
                create:
                    false);

        if (eventIndex >= 0
            && ledger is not null
            && ledger.VisibilityByEventIndex
                .TryGetValue(
                    eventIndex,
                    out var stored))
        {
            return stored;
        }

        // Pre-rework saves have no historical visibility ledger. For those
        // events, use the best classification available from current state.
        return EvaluateFamilyNewsNow(
            gameEvent);
    }

    internal void RecordFamilyNewsVisibility(
        GameEvent gameEvent)
    {
        var index =
            IndexOfEvent(
                gameEvent);

        if (index < 0)
            return;

        var ledger =
            GetNewsLedger(
                create:
                    true);

        if (ledger is null)
            return;

        ledger.VisibilityByEventIndex[index] =
            EvaluateFamilyNewsNow(
                gameEvent);
    }

    private bool EvaluateFamilyNewsNow(
        GameEvent gameEvent)
    {
        if (gameEvent.Data.TryGetValue(
                "suppressChronicle",
                out var suppress)
            && suppress.Equals(
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var involved =
            new List<Guid>();

        if (gameEvent.SubjectId
            is Guid subjectId)
        {
            involved.Add(
                subjectId);
        }

        involved.AddRange(
            gameEvent.RelatedPersonIds);

        var involvedPeople =
            involved
                .Distinct()
                .Select(
                    FindPerson)
                .Where(
                    person =>
                        person is not null)
                .Cast<IPerson>()
                .ToList();

        if (involvedPeople.Any(
            person =>
                GetHouseholdInfo(
                    person)?.Class
                    == HouseholdClass.Lineage))
        {
            return true;
        }

        if (!involvedPeople.Any(
            person =>
                _family.IsBloodline(
                    person)
                || person.Tags.Has(
                    "simulation.peripheral_ex")
                || person.Tags.Has(
                    "simulation.peripheral_partner")
                || HasDirectBloodlineMarriage(
                    person)))
        {
            return false;
        }

        var type =
            gameEvent.Type;

        if (type.Equals(
                "life.birth",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "peripheral.birth",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (type.Equals(
                "relationship.married",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.remarried",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.partnered",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.low_satisfaction_divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.prison_divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.affair",
                StringComparison.OrdinalIgnoreCase))
        {
            // The affair event is itself the divorce event in the current
            // relationship mechanic, so it belongs to the permitted
            // marriage/divorce family-news category.
            return true;
        }

        if (type.Equals(
                "health.serious_illness",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "health.natural_recovery",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "health.second_wind",
                StringComparison.OrdinalIgnoreCase))
        {
            return gameEvent.Data.TryGetValue(
                    "familyNews",
                    out var familyNews)
                && familyNews.Equals(
                    "true",
                    StringComparison.OrdinalIgnoreCase);
        }

        if (type.Equals(
                "justice.crime",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "justice.released",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return type.StartsWith(
            "rare.",
            StringComparison.OrdinalIgnoreCase);
    }

    private int IndexOfEvent(
        GameEvent gameEvent)
    {
        for (var index = 0;
            index < _events.AllEvents.Count;
            index++)
        {
            if (ReferenceEquals(
                _events.AllEvents[index],
                gameEvent))
            {
                return index;
            }
        }

        return -1;
    }

    private FamilyNewsLedgerComponent? GetNewsLedger(
        bool create)
    {
        var anchor =
            _gameState.People
                .Where(
                    person =>
                        _family.IsBloodline(
                            person))
                .OrderBy(
                    person =>
                        _family.GetGeneration(
                            person)
                        ?? int.MaxValue)
                .ThenBy(
                    BirthSortKey)
                .FirstOrDefault();

        if (anchor is null)
            return null;

        var ledger =
            anchor.Components.Get<
                FamilyNewsLedgerComponent>();

        if (ledger is not null
            || !create)
        {
            return ledger;
        }

        ledger =
            new FamilyNewsLedgerComponent();

        anchor.Components.Set(
            ledger);

        return ledger;
    }

    internal void UpdatePeripheralRelationshipState(
        GameEvent gameEvent)
    {
        var isBreakup =
            gameEvent.Type.Equals(
                "relationship.divorce",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "relationship.low_satisfaction_divorce",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "relationship.prison_divorce",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "relationship.affair",
                StringComparison.OrdinalIgnoreCase);

        if (isBreakup)
        {
            var involved =
                new List<IPerson>();

            if (gameEvent.SubjectId
                is Guid subjectId)
            {
                var subject =
                    FindPerson(
                        subjectId);

                if (subject is not null)
                {
                    involved.Add(
                        subject);
                }
            }

            involved.AddRange(
                gameEvent.RelatedPersonIds
                    .Select(
                        FindPerson)
                    .Where(
                        person =>
                            person is not null)
                    .Cast<IPerson>());

            var ex =
                involved.FirstOrDefault(
                    person =>
                        person.Tags.Has(
                            "simulation.peripheral_ex"));

            var laterPartner =
                involved.FirstOrDefault(
                    person =>
                        person.Tags.Has(
                            "simulation.peripheral_partner"));

            if (ex is not null
                && laterPartner is not null)
            {
                laterPartner.Tags.Remove(
                    "simulation.peripheral_partner");

                laterPartner.Tags.Add(
                    "simulation.peripheral_inactive");
            }

            foreach (var formerPartner in
                involved
                    .Where(
                        person =>
                            !_family.IsBloodline(
                                person)
                            && HasDirectBloodlineMarriage(
                                person))
                    .DistinctBy(
                        person =>
                            person.Id)
                    .ToList())
            {
                DetachFormerPartnerAfterBreakup(
                    formerPartner,
                    involved);
            }

            return;
        }

        if (!gameEvent.Type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.SubjectId
                is not Guid deceasedId)
        {
            return;
        }

        var deceased =
            FindPerson(
                deceasedId);

        if (deceased is null
            || !deceased.Tags.Has(
                "simulation.peripheral_ex"))
        {
            return;
        }

        var laterPartners =
            gameEvent.RelatedPersonIds
                .Select(
                    FindPerson)
                .Where(
                    person =>
                        person is not null
                        && person.Tags.Has(
                            "simulation.peripheral_partner"))
                .Cast<IPerson>()
                .ToList();

        var currentPartner =
            _family.GetSpouse(
                deceased);

        if (currentPartner is not null
            && currentPartner.Tags.Has(
                "simulation.peripheral_partner")
            && !laterPartners.Any(
                partner =>
                    partner.Id
                    == currentPartner.Id))
        {
            laterPartners.Add(
                currentPartner);
        }

        foreach (var related in
            laterPartners)
        {
            related.Tags.Remove(
                "simulation.peripheral_partner");

            related.Tags.Add(
                "simulation.peripheral_inactive");
        }
    }

    private void CollapseInvalidMinorHeadHouseholds()
    {
        foreach (var child in
            _gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive")
                        && person.Age < 18
                        && _family.IsBloodline(
                            person)
                        && _economy.HasHousehold(
                            person))
                .ToList())
        {
            var finance =
                _economy.GetHousehold(
                    child);

            if (finance is null)
                continue;

            // Living minors must never head dynasty households. Older
            // saves could acquire such components because a read-only
            // economy lookup used to auto-create male-lineage households.
            // Preserve any assets on the corrupt household as the child's
            // pending inheritance before dissolving it.
            var wealth =
                finance.Wealth;

            var houses =
                _economy
                    .TakeAllHouses(
                        child)
                    .ToList();

            if (wealth > 0)
            {
                _economy.ChangePendingInheritance(
                    child,
                    wealth);

                _economy.SetWealth(
                    child,
                    0);
            }

            foreach (var house in
                houses)
            {
                _economy.AddPendingHouse(
                    child,
                    house);
            }

            _economy.DissolveHousehold(
                child);

            child.Tags.Remove(
                "household.independent_orphan");

            child.Tags.Remove(
                "residence.independent");

            var mother =
                _family.GetMother(
                    child);

            var father =
                _family.GetFather(
                    child);

            var parentHead =
                mother is not null
                && mother.Tags.Has(
                    "state.alive")
                    ? ResolveHouseholdHead(
                        mother)
                    : null;

            parentHead ??=
                father is not null
                && father.Tags.Has(
                    "state.alive")
                    ? ResolveHouseholdHead(
                        father)
                    : null;

            if (parentHead is not null)
            {
                _economy.AddHouseholdMember(
                    parentHead,
                    child);
            }
        }
    }

    private void DissolveEmptyDeadHouseholdTombstones()
    {
        foreach (var head in
            _gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.dead")
                        && _economy.HasHousehold(
                            person))
                .ToList())
        {
            var finance =
                _economy.GetHousehold(
                    head);

            if (finance is null
                || finance.Wealth != 0
                || finance.HousesOwned != 0
                || finance.NannyId is not null
                || _economy
                    .GetHostedDependentIds(
                        head)
                    .Count > 0)
            {
                continue;
            }

            var hasLivingMembers =
                _economy
                    .GetHouseholdMemberIds(
                        head)
                    .Select(
                        FindPerson)
                    .Any(
                        member =>
                            member is not null
                            && member.Tags.Has(
                                "state.alive"));

            if (!hasLivingMembers)
            {
                _economy.DissolveHousehold(
                    head);
            }
        }
    }

    private void SeedLegacyHouseholdMemberships()
    {
        foreach (var head in
            _gameState.People
                .Where(
                    person =>
                        _economy.HasHousehold(
                            person))
                .ToList())
        {
            if (_economy.IsLegacyMembershipSeeded(
                head))
            {
                continue;
            }

            var memberIds =
                _economy
                    .GetHouseholdMemberIds(
                        head)
                    .ToHashSet();

            var spouse =
                _family.GetSpouse(
                    head);

            spouse ??=
                _family.GetRelationshipHistory(
                    head)
                .Where(
                    relationship =>
                        relationship.EndYear is null
                        || relationship.EndReason?.Equals(
                            "death",
                            StringComparison.OrdinalIgnoreCase)
                            == true)
                .OrderByDescending(
                    relationship =>
                        relationship.StartYear)
                .Select(
                    relationship =>
                        FindPerson(
                            relationship.SpouseId))
                .FirstOrDefault(
                    candidate =>
                        candidate is not null
                        && candidate.Tags.Has(
                            "state.alive"));

            if (spouse is not null
                && spouse.Tags.Has(
                    "state.alive")
                && !memberIds.Contains(
                    spouse.Id))
            {
                _economy.AddHouseholdMember(
                    head,
                    spouse);

                memberIds.Add(
                    spouse.Id);
            }

            // Old saves did not have autonomous female-branch households.
            // Reconstruct the family unit conservatively: living unmarried
            // children remain with the old household unless they already
            // had a real household of their own in the save.
            foreach (var child in
                _family.GetChildren(
                    head)
                .Where(
                    child =>
                        child.Tags.Has(
                            "state.alive")
                        && _family.GetSpouse(
                            child) is null))
            {
                if (_economy.HasHousehold(
                        child)
                    || memberIds.Contains(
                        child.Id))
                {
                    continue;
                }

                _economy.AddHouseholdMember(
                    head,
                    child);

                memberIds.Add(
                    child.Id);
            }

            _economy.MarkLegacyMembershipSeeded(
                head);
        }
    }

    private void EnsureAdultBloodlineHouseholds()
    {
        foreach (var person in
            _gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive")
                        && _family.IsBloodline(
                            person)
                        && person.Age >= 18
                        && _family.GetSex(
                            person) == Sex.Male)
                .ToList())
        {
            if (_economy.HasHousehold(
                person))
            {
                continue;
            }

            var currentHead =
                ResolveHouseholdHead(
                    person);

            var spouse =
                _family.GetSpouse(
                    person);

            var explicitlyIndependent =
                person.Tags.Has(
                    "residence.independent")
                || person.Tags.Has(
                    "household.independent_orphan");

            // Newly reaching adulthood still creates the autonomous
            // household required by the new design. Older adults loaded
            // from a pre-rework save are not retroactively split from
            // their parents unless the old state already marks them as
            // independent or they have since married.
            var shouldBecomeIndependent =
                currentHead is null
                || person.Age == 18
                || explicitlyIndependent
                || spouse is not null;

            if (!shouldBecomeIndependent)
            {
                continue;
            }

            if (currentHead is not null)
            {
                _economy.RemoveHouseholdMember(
                    person);

                if (spouse is not null
                    && ResolveHouseholdHead(
                        spouse)?.Id
                        == currentHead.Id)
                {
                    _economy.RemoveHouseholdMember(
                        spouse);
                }
            }

            _economy.EnsureIndependentHousehold(
                person,
                person);

            if (spouse is not null
                && spouse.Tags.Has(
                    "state.alive"))
            {
                _economy.AddHouseholdMember(
                    person,
                    spouse);
            }
        }
    }

    private void ReconcileMarriedBloodlineWomen()
    {
        foreach (var woman in
            _gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive")
                        && _family.IsBloodline(
                            person)
                        && _family.GetSex(
                            person) == Sex.Female
                        && person.Age >= 18)
                .ToList())
        {
            var husband =
                _family.GetSpouse(
                    woman);

            if (husband is null
                || !husband.Tags.Has(
                    "state.alive"))
            {
                continue;
            }

            var womanHead =
                ResolveHouseholdHead(
                    woman);

            var husbandHead =
                ResolveHouseholdHead(
                    husband);

            if (womanHead is not null
                && womanHead.Id == woman.Id
                && !_family.IsMaleLineage(
                    woman)
                && husbandHead is null)
            {
                _economy.TransferHouseholdHead(
                    woman,
                    husband);

                _economy.AddHouseholdMember(
                    husband,
                    woman);

                AttachUnmarriedChildrenToMarriedHousehold(
                    woman,
                    husband);

                continue;
            }

            if (womanHead is not null
                && husbandHead?.Id
                    == womanHead.Id)
            {
                AttachUnmarriedChildrenToMarriedHousehold(
                    woman,
                    womanHead);

                continue;
            }

            if (husbandHead is not null
                && husbandHead.Id
                    == husband.Id)
            {
                _economy.AddHouseholdMember(
                    husband,
                    woman);

                AttachUnmarriedChildrenToMarriedHousehold(
                    woman,
                    husband);

                continue;
            }

            _economy.RemoveHouseholdMember(
                woman);

            _economy.RemoveHouseholdMember(
                husband);

            _economy.EnsureIndependentHousehold(
                husband,
                woman);

            _economy.AddHouseholdMember(
                husband,
                woman);

            AttachUnmarriedChildrenToMarriedHousehold(
                woman,
                husband);
        }
    }

    private void AttachUnmarriedChildrenToMarriedHousehold(
        IPerson bloodlineWoman,
        IPerson householdHead)
    {
        foreach (var child in
            _family.GetChildren(
                bloodlineWoman)
            .Where(
                child =>
                    child.Tags.Has(
                        "state.alive")
                    && _family.GetSpouse(
                        child) is null)
            .OrderBy(
                BirthSortKey)
            .ThenBy(
                child =>
                    child.Id))
        {
            if (_economy.HasHousehold(
                child))
            {
                if (!TryCollapseSyntheticLegacyChildHousehold(
                    child,
                    householdHead))
                {
                    continue;
                }
            }

            if (ResolveHouseholdHead(
                    child)?.Id
                == householdHead.Id)
            {
                continue;
            }

            _economy.AddHouseholdMember(
                householdHead,
                child);
        }
    }

    private bool TryCollapseSyntheticLegacyChildHousehold(
        IPerson child,
        IPerson parentHouseholdHead)
    {
        if (_family.IsMaleLineage(
                child)
            || _family.GetSex(
                child) != Sex.Male
            || child.Age <= 18
            || _family.GetSpouse(
                child) is not null
            || child.Tags.Has(
                "residence.independent")
            || child.Tags.Has(
                "household.independent_orphan"))
        {
            return false;
        }

        var finance =
            _economy.GetHousehold(
                child);

        if (finance is null
            || finance.Wealth != 0
            || finance.HousesOwned != 0
            || finance.NannyId is not null
            || _economy
                .GetHostedDependentIds(
                    child)
                .Count > 0
            || _economy
                .GetHouseholdMemberIds(
                    child)
                .Count > 1)
        {
            return false;
        }

        _economy.DissolveHousehold(
            child);

        _economy.AddHouseholdMember(
            parentHouseholdHead,
            child);

        return true;
    }

    private void ReconcileCurrentSpouses()
    {
        foreach (var bloodline in
            _gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive")
                        && _family.IsBloodline(
                            person))
                .ToList())
        {
            var spouse =
                _family.GetSpouse(
                    bloodline);

            if (spouse is null
                || !spouse.Tags.Has(
                    "state.alive"))
            {
                continue;
            }

            var head =
                ResolveHouseholdHead(
                    bloodline);

            if (head is null)
                continue;

            _economy.AddHouseholdMember(
                head,
                spouse);
        }
    }

    private void ReconcileBloodlineDependents()
    {
        foreach (var person in
            _gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive")
                        && _family.IsBloodline(
                            person))
                .ToList())
        {
            if (_economy.HasHousehold(
                person))
            {
                continue;
            }

            if (person.Age >= 18
                && _family.GetSex(
                    person) == Sex.Male)
            {
                continue;
            }

            if (person.Age >= 18
                && _family.GetSex(
                    person) == Sex.Female
                && _family.GetSpouse(
                    person) is not null)
            {
                continue;
            }

            var current =
                ResolveHouseholdHead(
                    person);

            if (current is not null
                && current.Tags.Has(
                    "state.alive"))
            {
                continue;
            }

            var mother =
                _family.GetMother(
                    person);

            var father =
                _family.GetFather(
                    person);

            var parentHead =
                mother is not null
                && mother.Tags.Has(
                    "state.alive")
                    ? ResolveHouseholdHead(
                        mother)
                    : null;

            parentHead ??=
                father is not null
                && father.Tags.Has(
                    "state.alive")
                    ? ResolveHouseholdHead(
                        father)
                    : null;

            if (parentHead is not null)
            {
                _economy.AddHouseholdMember(
                    parentHead,
                    person);

                continue;
            }

            var host =
                _gameState.People
                    .FirstOrDefault(
                        candidate =>
                            _economy.HasHousehold(
                                candidate)
                            && _economy
                                .GetHostedDependentIds(
                                    candidate)
                                .Contains(
                                    person.Id));

            if (host is not null)
            {
                _economy.AddHouseholdMember(
                    host,
                    person);

                continue;
            }

            if (person.Age >= 18)
            {
                _economy.EnsureIndependentHousehold(
                    person,
                    person);
            }
        }
    }

    private void AttachSurvivingUnmarriedParentsToDeadHeadHouseholds()
    {
        foreach (var deadHead in
            _gameState.People
                .Where(
                    person =>
                        _economy.HasHousehold(
                            person)
                        && person.Tags.Has(
                            "state.dead"))
                .ToList())
        {
            var members =
                _economy
                    .GetHouseholdMemberIds(
                        deadHead)
                    .Select(
                        FindPerson)
                    .Where(
                        person =>
                            person is not null)
                    .Cast<IPerson>()
                    .ToList();

            foreach (var child in
                members.Where(
                    member =>
                        member.Tags.Has(
                            "state.alive")
                        && member.Age < 18
                        && _family.IsBloodline(
                            member)))
            {
                var survivingParent =
                    _family.GetFather(
                        child)?.Id
                        == deadHead.Id
                        ? _family.GetMother(
                            child)
                        : _family.GetMother(
                            child)?.Id
                            == deadHead.Id
                            ? _family.GetFather(
                                child)
                            : null;

                if (survivingParent is null
                    || !survivingParent.Tags.Has(
                        "state.alive")
                    || _family.GetSpouse(
                        survivingParent)
                        is not null)
                {
                    continue;
                }

                // This preserves the divorced-parent custody rule: an
                // unmarried surviving biological parent can return to the
                // dead parent's household while raising their bloodline
                // minor, without being moved into an uncle's household.
                survivingParent.Tags.Add(
                    "simulation.peripheral_ex");

                survivingParent.Tags.Remove(
                    "simulation.peripheral_detached");

                _economy.AddHouseholdMember(
                    deadHead,
                    survivingParent);
            }
        }
    }

    private void DetachFormerPartnerAfterBreakup(
        IPerson formerPartner,
        IReadOnlyList<IPerson> involved)
    {
        formerPartner.Tags.Add(
            "simulation.peripheral_ex");

        formerPartner.Tags.Add(
            "simulation.peripheral_detached");

        var householdHead =
            ResolveHouseholdHead(
                formerPartner);

        if (householdHead is null)
            return;

        if (householdHead.Id
            == formerPartner.Id)
        {
            var successor =
                involved
                    .Where(
                        person =>
                            person.Tags.Has(
                                "state.alive")
                            && _family.IsBloodline(
                                person)
                            && ResolveHouseholdHead(
                                person)?.Id
                                == formerPartner.Id)
                    .OrderBy(
                        person =>
                            person.Id)
                    .FirstOrDefault();

            successor ??=
                _economy
                    .GetHouseholdMemberIds(
                        formerPartner)
                    .Select(
                        FindPerson)
                    .Where(
                        person =>
                            person is not null
                            && person.Tags.Has(
                                "state.alive")
                            && person.Age >= 18
                            && _family.IsBloodline(
                                person))
                    .Cast<IPerson>()
                    .OrderBy(
                        BirthSortKey)
                    .FirstOrDefault();

            if (successor is not null)
            {
                _economy.TransferHouseholdHead(
                    formerPartner,
                    successor);
            }
        }

        // TransferHouseholdHead already removes the old head from MemberIds.
        // For ordinary ex-spouses this removes the member directly.
        _economy.RemoveHouseholdMember(
            formerPartner);
    }

    private void CleanupFormerPartners()
    {
        foreach (var head in
            _gameState.People
                .Where(
                    person =>
                        _economy.HasHousehold(
                            person))
                .ToList())
        {
            var members =
                _economy
                    .GetHouseholdMemberIds(
                        head)
                    .Select(
                        FindPerson)
                    .Where(
                        person =>
                            person is not null)
                    .Cast<IPerson>()
                    .ToList();

            foreach (var member in
                members.Where(
                    member =>
                        member.Tags.Has(
                            "state.alive")
                        && !_family.IsBloodline(
                            member)
                        && member.Id
                            != head.Id)
                    .ToList())
            {
                if (IsCurrentSpouseOfBloodline(
                    member))
                {
                    continue;
                }

                if (HasDirectBloodlineMarriage(
                    member))
                {
                    member.Tags.Add(
                        "simulation.peripheral_ex");

                    member.Tags.Add(
                        "simulation.peripheral_detached");
                }

                _economy.RemoveHouseholdMember(
                    member);
            }
        }
    }

    private void TagPeripheralFormerPartners()
    {
        foreach (var person in
            _gameState.People
                .Where(
                    candidate =>
                        candidate.Tags.Has(
                            "state.alive")
                        && !_family.IsBloodline(
                            candidate)
                        && HasDirectBloodlineMarriage(
                            candidate))
                .ToList())
        {
            if (IsCurrentSpouseOfBloodline(
                person))
            {
                person.Tags.Remove(
                    "simulation.peripheral_ex");

                person.Tags.Remove(
                    "simulation.peripheral_detached");

                continue;
            }

            // Peripheral relationship status is separate from household
            // caregiving. A divorced/widowed former spouse may still head or
            // belong to a Bloodline household while bloodline children depend
            // on them, but any later marriage remains peripheral and any
            // children from that later union are news-only.
            person.Tags.Add(
                "simulation.peripheral_ex");

            var laterPartner =
                _family.GetSpouse(
                    person);

            if (laterPartner is not null
                && !_family.IsBloodline(
                    laterPartner))
            {
                laterPartner.Tags.Add(
                    "simulation.peripheral_partner");
            }
        }
    }

    private void ReconcileHeadSuccession()
    {
        foreach (var oldHead in
            _gameState.People
                .Where(
                    person =>
                        _economy.HasHousehold(
                            person))
                .ToList())
        {
            var memberIds =
                _economy.GetHouseholdMemberIds(
                    oldHead);

            var livingMembers =
                memberIds
                    .Select(
                        FindPerson)
                    .Where(
                        member =>
                            member is not null
                            && member.Tags.Has(
                                "state.alive"))
                    .Cast<IPerson>()
                    .ToList();

            var livingBloodline =
                livingMembers
                    .Where(
                        member =>
                            _family.IsBloodline(
                                member))
                    .ToList();

            if (livingBloodline.Count == 0)
            {
                foreach (var survivor in
                    livingMembers.Where(
                        member =>
                            !_family.IsBloodline(
                                member)))
                {
                    if (HasDirectBloodlineMarriage(
                        survivor))
                    {
                        survivor.Tags.Add(
                            "simulation.peripheral_ex");

                        survivor.Tags.Add(
                            "simulation.peripheral_detached");
                    }
                }

                _economy.MarkEstateReady(
                    oldHead);

                continue;
            }

            if (oldHead.Tags.Has(
                "state.alive"))
            {
                if (!_family.IsBloodline(
                        oldHead)
                    && !IsCurrentSpouseOfBloodline(
                        oldHead))
                {
                    var bloodlineSuccessor =
                        livingBloodline
                            .Where(
                                member =>
                                    member.Age >= 18)
                            .OrderBy(
                                BirthSortKey)
                            .ThenBy(
                                member =>
                                    member.Id)
                            .FirstOrDefault();

                    if (bloodlineSuccessor is not null)
                    {
                        oldHead.Tags.Add(
                            "simulation.peripheral_ex");

                        oldHead.Tags.Add(
                            "simulation.peripheral_detached");

                        _economy.TransferHouseholdHead(
                            oldHead,
                            bloodlineSuccessor);

                        continue;
                    }

                    // The only case in which a living former spouse may
                    // remain head is the explicit custody situation where
                    // they are the sole adult caring for bloodline minors.
                    if (!IsNeededByBloodlineMinor(
                        oldHead,
                        oldHead))
                    {
                        _economy.MarkEstateReady(
                            oldHead);
                    }
                }

                continue;
            }

            var successor =
                FindLivingFormerSpouse(
                    oldHead,
                    livingMembers)
                ?? FindContinuingBloodlineCaregiver(
                    livingMembers);

            if (successor is null)
            {
                _economy.MarkEstateReady(
                    oldHead);

                continue;
            }

            _economy.TransferHouseholdHead(
                oldHead,
                successor);
        }
    }

    private IPerson? FindContinuingBloodlineCaregiver(
        IReadOnlyList<IPerson> livingMembers)
    {
        var minors =
            livingMembers
                .Where(
                    member =>
                        member.Age < 18
                        && _family.IsBloodline(
                            member))
                .ToList();

        if (minors.Count == 0)
            return null;

        return livingMembers
            .Where(
                candidate =>
                    candidate.Age >= 18
                    && minors.Any(
                        child =>
                            _family.GetMother(
                                child)?.Id
                                == candidate.Id
                            || _family.GetFather(
                                child)?.Id
                                == candidate.Id))
            .OrderBy(
                BirthSortKey)
            .FirstOrDefault();
    }

    private IPerson? FindLivingFormerSpouse(
        IPerson oldHead,
        IReadOnlyList<IPerson> livingMembers)
    {
        var memberIds =
            livingMembers
                .Select(
                    member =>
                        member.Id)
                .ToHashSet();

        foreach (var history in
            _family.GetRelationshipHistory(
                oldHead)
                .Where(
                    relationship =>
                        relationship.EndYear is null
                        || relationship.EndReason?.Equals(
                            "death",
                            StringComparison.OrdinalIgnoreCase)
                            == true)
                .OrderByDescending(
                    relationship =>
                        relationship.StartYear))
        {
            if (!memberIds.Contains(
                history.SpouseId))
            {
                continue;
            }

            var spouse =
                FindPerson(
                    history.SpouseId);

            if (spouse is not null
                && spouse.Tags.Has(
                    "state.alive"))
            {
                return spouse;
            }
        }

        return null;
    }

    private IReadOnlyList<IPerson> GetLivingMembers(
        IPerson householdRepresentative)
    {
        return _economy
            .GetHouseholdMemberIds(
                householdRepresentative)
            .Select(
                FindPerson)
            .Where(
                member =>
                    member is not null
                    && member.Tags.Has(
                        "state.alive"))
            .Cast<IPerson>()
            .DistinctBy(
                member =>
                    member.Id)
            .ToList();
    }

    private bool IsCurrentSpouseOfBloodline(
        IPerson person)
    {
        var spouse =
            _family.GetSpouse(
                person);

        return spouse is not null
            && spouse.Tags.Has(
                "state.alive")
            && _family.IsBloodline(
                spouse);
    }

    private bool IsNeededByBloodlineMinor(
        IPerson person,
        IPerson householdHead)
    {
        return _economy
            .GetHouseholdMemberIds(
                householdHead)
            .Select(
                FindPerson)
            .Where(
                member =>
                    member is not null
                    && member.Tags.Has(
                        "state.alive")
                    && member.Age < 18
                    && _family.IsBloodline(
                        member))
            .Cast<IPerson>()
            .Any(
                child =>
                    _family.GetMother(
                        child)?.Id
                        == person.Id
                    || _family.GetFather(
                        child)?.Id
                        == person.Id);
    }

    private bool HasDirectBloodlineMarriage(
        IPerson person)
    {
        return _family.GetRelationshipHistory(
                person)
            .Any(
                history =>
                {
                    var spouse =
                        FindPerson(
                            history.SpouseId);

                    return spouse is not null
                        && _family.IsBloodline(
                            spouse);
                });
    }

    private long BirthSortKey(
        IPerson person)
    {
        var year =
            person.BirthDate?.Year
            ?? (
                _gameState.Year
                - person.Age
            );

        var month =
            person.BirthDate?.Month
            ?? 1;

        var day =
            person.BirthDate?.Day
            ?? 1;

        return year * 10000L
            + month * 100L
            + day;
    }

    private IPerson? FindPerson(
        Guid id)
    {
        return _gameState.People
            .FirstOrDefault(
                person =>
                    person.Id
                    == id);
    }
}
