using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed partial class StandardHouseholdService
{
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

        var householdHead =
            ResolveHouseholdHead(
                formerPartner);

        if (householdHead is null)
        {
            formerPartner.Tags.Add(
                "simulation.peripheral_detached");

            return;
        }

        // A former partner who is now the autonomous head caring for a
        // Bloodline minor must remain attached to that household. Custody is
        // authoritative household membership; the generic breakup cleanup
        // must not immediately undo the household created by Relationships.
        if (householdHead.Id
                == formerPartner.Id
            && IsNeededByBloodlineMinor(
                formerPartner,
                householdHead))
        {
            formerPartner.Tags.Remove(
                "simulation.peripheral_detached");

            return;
        }

        formerPartner.Tags.Add(
            "simulation.peripheral_detached");

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

                // After a Bloodline household head dies, EndRelationship
                // has already cleared the current spouse link. Keep the
                // surviving parent attached when they are still caring for
                // a Bloodline minor so head succession can transfer this
                // household to the widow/widower instead of detaching them.
                if (IsNeededByBloodlineMinor(
                    member,
                    head))
                {
                    member.Tags.Add(
                        "simulation.peripheral_ex");

                    member.Tags.Remove(
                        "simulation.peripheral_detached");

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
        var living =
            _gameState.People
                .Where(person =>
                    person.Tags.Has("state.alive"))
                .ToList();

        // Tier 1: every living current/former partner of a Bloodline person is
        // permanently simulated. Direct former partners keep the peripheral_ex
        // tag even if the Bloodline relationship ended years ago.
        var directPartners =
            living
                .Where(person =>
                    !_family.IsBloodline(person)
                    && HasDirectBloodlineMarriage(person))
                .ToList();

        foreach (var person in directPartners)
        {
            person.Tags.Remove(
                SimulationState.PeripheralInactiveTag);

            person.Tags.Remove(
                "simulation.peripheral_partner");

            if (IsCurrentSpouseOfBloodline(person))
            {
                person.Tags.Remove(
                    "simulation.peripheral_ex");

                person.Tags.Remove(
                    "simulation.peripheral_detached");
            }
            else
            {
                person.Tags.Add(
                    "simulation.peripheral_ex");
            }
        }

        var directPartnerIds =
            directPartners
                .Select(person => person.Id)
                .ToHashSet();

        // Tier 2: the current spouse of a direct former partner is simulated
        // only for as long as that marriage remains current.
        var temporaryPartners =
            new HashSet<Guid>();

        foreach (var formerPartner in
            directPartners.Where(person =>
                person.Tags.Has(
                    "simulation.peripheral_ex")))
        {
            var spouse =
                _family.GetSpouse(formerPartner);

            if (spouse is null
                || !spouse.Tags.Has("state.alive")
                || _family.IsBloodline(spouse)
                || directPartnerIds.Contains(spouse.Id))
            {
                continue;
            }

            spouse.Tags.Add(
                "simulation.peripheral_partner");

            spouse.Tags.Remove(
                SimulationState.PeripheralInactiveTag);

            temporaryPartners.Add(
                spouse.Id);
        }

        // A former temporary spouse (the ex-partner of an ex-partner) stops
        // simulation once that relationship ends. Keep the record for history.
        foreach (var person in living.Where(person =>
            person.Tags.Has(
                "simulation.peripheral_partner")
            && !temporaryPartners.Contains(person.Id)))
        {
            person.Tags.Remove(
                "simulation.peripheral_partner");

            if (!_family.IsBloodline(person)
                && !HasDirectBloodlineMarriage(person))
            {
                person.Tags.Add(
                    SimulationState.PeripheralInactiveTag);
            }
        }

        // Older saves may contain fully simulated children from a later
        // peripheral marriage. They are not Bloodline relatives and should be
        // retained only as historical records, matching new peripheral births.
        foreach (var child in living.Where(person =>
            !_family.IsBloodline(person)
            && !HasDirectBloodlineMarriage(person)
            && !temporaryPartners.Contains(person.Id)))
        {
            var father =
                _family.GetFather(child);

            var mother =
                _family.GetMother(child);

            if (father is null
                && mother is null)
            {
                continue;
            }

            var hasBloodlineParent =
                (father is not null && _family.IsBloodline(father))
                || (mother is not null && _family.IsBloodline(mother));

            if (hasBloodlineParent)
                continue;

            var peripheralParent =
                (father is not null
                    && (father.Tags.Has("simulation.peripheral_ex")
                        || father.Tags.Has("simulation.peripheral_partner")))
                || (mother is not null
                    && (mother.Tags.Has("simulation.peripheral_ex")
                        || mother.Tags.Has("simulation.peripheral_partner")));

            if (peripheralParent)
            {
                child.Tags.Add(
                    SimulationState.PeripheralInactiveTag);
            }
        }
    }

}
