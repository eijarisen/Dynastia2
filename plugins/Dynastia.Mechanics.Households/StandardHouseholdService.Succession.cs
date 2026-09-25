using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed partial class StandardHouseholdService
{
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
                                "state.alive")
                            // MemberIds can be transiently stale after an
                            // action moves someone into their own household.
                            // A person who already heads another household is
                            // no longer a resident successor of this one.
                            && (member.Id == oldHead.Id
                                || !_economy.HasHousehold(member)))
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
                // A living divorced spouse may head a non-playable peripheral
                // household even after no Bloodline resident remains there.
                // That residence must stay active so finances and chronicle
                // ownership remain attached to the person instead of a child or
                // other relative elsewhere.
                if (oldHead.Tags.Has("state.alive")
                    && !_family.IsBloodline(oldHead)
                    && HasDirectBloodlineMarriage(oldHead))
                {
                    oldHead.Tags.Add(
                        "simulation.peripheral_ex");
                    oldHead.Tags.Remove(
                        "simulation.peripheral_detached");
                    _economy.MarkEstateReady(
                        oldHead,
                        false);
                    continue;
                }

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

            var adultMaleLineageResident =
                FindOldestAdultMaleLineageResident(
                    livingMembers);

            if (adultMaleLineageResident is null
                && oldHead.Tags.Has("state.alive")
                && !(_family.GetSex(oldHead) == Sex.Male
                    && _family.IsMaleLineage(oldHead))
                && FindResidentFormerSpouseParent(livingMembers) is IPerson formerSpouseParent
                && formerSpouseParent.Id != oldHead.Id)
            {
                _economy.TransferHouseholdHead(
                    oldHead,
                    formerSpouseParent);

                _economy.AddHouseholdMember(
                    formerSpouseParent,
                    oldHead);

                continue;
            }

            if (oldHead.Tags.Has(
                    "state.alive")
                && !SimulationState.IsInactive(
                    oldHead))
            {
                var oldHeadIsEligibleLineageHead =
                    oldHead.Age >= 18
                    && !oldHead.Tags.Has("vocation.religious.active")
                    && _family.GetSex(
                        oldHead) == Sex.Male
                    && _family.IsMaleLineage(
                        oldHead);

                // A surviving widow/caregiver may temporarily head a lineage
                // household while all male-lineage sons are minors. As soon
                // as the oldest resident son is an adult, headship passes to
                // him and the caregiver remains a resident member.
                if (!oldHeadIsEligibleLineageHead
                    && adultMaleLineageResident is not null
                    && adultMaleLineageResident.Id
                        != oldHead.Id)
                {
                    _economy.TransferHouseholdHead(
                        oldHead,
                        adultMaleLineageResident);

                    _economy.AddHouseholdMember(
                        adultMaleLineageResident,
                        oldHead);

                    continue;
                }

                if (!_family.IsBloodline(
                        oldHead)
                    && !IsCurrentSpouseOfBloodline(
                        oldHead))
                {
                    // A divorced parent who established a custody household
                    // remains that household's named head. Do not immediately
                    // rename the household after an adult daughter or other
                    // non-lineage Bloodline resident. Adult male-lineage
                    // succession above still takes priority.
                    if (IsFormerSpouseWithResidentChild(
                            oldHead,
                            livingMembers))
                    {
                        continue;
                    }

                    var bloodlineSuccessor =
                        livingBloodline
                            .Where(
                                member =>
                                    member.Age >= 18
                                    && !member.Tags.Has("vocation.religious.active"))
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
                adultMaleLineageResident
                ?? FindLivingFormerSpouse(
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

    private IPerson? FindOldestAdultMaleLineageResident(
        IReadOnlyList<IPerson> livingMembers)
    {
        return livingMembers
            .Where(
                candidate =>
                    candidate.Age >= 18
                    && _family.GetSex(
                        candidate) == Sex.Male
                    && _family.IsMaleLineage(
                        candidate)
                    && !candidate.Tags.Has("vocation.religious.active")
                    && !SimulationState.IsInactive(
                        candidate))
            .OrderBy(
                BirthSortKey)
            .ThenBy(
                candidate =>
                    candidate.Id)
            .FirstOrDefault();
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
                    && !candidate.Tags.Has("vocation.religious.active")
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
                    "state.alive")
                && !spouse.Tags.Has("vocation.religious.active"))
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

    private IPerson? FindResidentFormerSpouseParent(
        IReadOnlyList<IPerson> livingMembers)
    {
        return livingMembers
            .Where(candidate =>
                candidate.Age >= 18
                && !candidate.Tags.Has("vocation.religious.active")
                && !SimulationState.IsInactive(candidate)
                && !_family.IsBloodline(candidate)
                && !IsCurrentSpouseOfBloodline(candidate)
                && HasEndedBloodlineMarriage(candidate)
                && livingMembers.Any(member =>
                    member.Id != candidate.Id
                    && _family.IsBloodline(member)
                    && (_family.GetMother(member)?.Id == candidate.Id
                        || _family.GetFather(member)?.Id == candidate.Id)))
            .OrderBy(BirthSortKey)
            .FirstOrDefault();
    }

    private bool HasEndedBloodlineMarriage(
        IPerson person) =>
        _family.GetRelationshipHistory(person)
            .Any(history =>
            {
                if (history.EndYear is null)
                    return false;

                var spouse = FindPerson(history.SpouseId);
                return spouse is not null
                    && _family.IsBloodline(spouse);
            });

    private bool IsFormerSpouseWithResidentChild(
        IPerson person,
        IReadOnlyList<IPerson> livingMembers)
    {
        if (!HasDirectBloodlineMarriage(person))
            return false;

        return livingMembers.Any(member =>
            member.Id != person.Id
            && (_family.GetMother(member)?.Id == person.Id
                || _family.GetFather(member)?.Id == person.Id));
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
