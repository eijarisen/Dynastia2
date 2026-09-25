using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed partial class StandardHouseholdService
{
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
                        && !person.Tags.Has("vocation.religious.active")
                        && _family.GetSex(
                            person) == Sex.Male)
                .ToList())
        {
            // Existing independent households are authoritative, including
            // adult sons loaded from older saves. Never merge them back into
            // a parental household merely because the new residence rules
            // would have kept them resident today.
            if (_economy.HasHousehold(
                person))
            {
                continue;
            }

            var currentHead =
                ResolveHouseholdHead(
                    person);

            var explicitlyIndependent =
                person.Tags.Has(
                    "residence.independent")
                || person.Tags.Has(
                    "household.independent_orphan");

            // Adulthood and marriage no longer establish a separate
            // household. A resident adult son stays where he is until a
            // mechanic explicitly makes him independent or succession makes
            // him the head of the existing household.
            if (currentHead is not null
                && !explicitlyIndependent)
            {
                continue;
            }

            if (currentHead is null
                && !explicitlyIndependent)
            {
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

                    var residentSpouse =
                        _family.GetSpouse(
                            person);

                    if (residentSpouse is not null
                        && residentSpouse.Tags.Has(
                            "state.alive"))
                    {
                        _economy.AddHouseholdMember(
                            parentHead,
                            residentSpouse);
                    }

                    continue;
                }
            }

            // With no valid household to remain in, an adult bloodline man
            // must still receive a household so orphaned/otherwise detached
            // branches cannot become permanently homeless.
            var spouse =
                _family.GetSpouse(
                    person);

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

            // Marriage residence follows the husband for heterosexual
            // marriages. Capture his pre-reconciliation home before a
            // household-head transfer can overwrite it with the wife's
            // former household residence.
            var husbandResidence =
                _locations.GetLocation(
                    husband)
                .HomeTown;

            var womanHouseholdIdBefore =
                _economy.GetHouseholdId(
                    woman);

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
                    husband,
                    womanHouseholdIdBefore);

                var transferredOrigin =
                    _economy.GetResidenceTown(husband);
                if (!transferredOrigin.Id.Equals(
                        husbandResidence.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _farming.SellOriginFarmlandForVoluntaryRelocation(
                        husband,
                        transferredOrigin,
                        husbandResidence);
                }

                _economy.SetResidenceTown(
                    husband,
                    husbandResidence);

                continue;
            }

            if (womanHead is not null
                && husbandHead?.Id
                    == womanHead.Id)
            {
                AttachUnmarriedChildrenToMarriedHousehold(
                    woman,
                    womanHead,
                    womanHouseholdIdBefore);

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
                    husband,
                    womanHouseholdIdBefore);

                _economy.SetResidenceTown(
                    husband,
                    husbandResidence);

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
                husband,
                womanHouseholdIdBefore);

            _economy.SetResidenceTown(
                husband,
                husbandResidence);
        }
    }

    private void AttachUnmarriedChildrenToMarriedHousehold(
        IPerson bloodlineWoman,
        IPerson householdHead,
        Guid? womanHouseholdIdBeforeMarriage)
    {
        var targetHouseholdId =
            _economy.GetHouseholdId(
                householdHead);

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
            var childHouseholdId =
                _economy.GetHouseholdId(
                    child);

            if (childHouseholdId is not null
                && childHouseholdId != targetHouseholdId
                && (womanHouseholdIdBeforeMarriage is null
                    || childHouseholdId != womanHouseholdIdBeforeMarriage))
            {
                // Custody/residence membership is authoritative. Remarriage
                // can carry along children who were actually living with the
                // woman, but it must never reclaim children resident with an
                // ex-partner merely because she is their biological mother.
                continue;
            }

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

            var spouseHead =
                ResolveHouseholdHead(
                    spouse);

            if (spouseHead?.Id
                == head.Id)
            {
                continue;
            }

            // Generic spouse reconciliation may move an ordinary member,
            // but it must never absorb the head of another active household.
            // Headship carries that household's assets, residence and
            // dependants and therefore requires an explicit transfer/merge
            // path rather than AddHouseholdMember(). Keep independently
            // headed households intact in this generic safety pass.
            if (_economy.HasHousehold(
                spouse))
            {
                continue;
            }

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
                    person) == Sex.Male
                && (person.Tags.Has(
                        "residence.independent")
                    || person.Tags.Has(
                        "household.independent_orphan")))
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

}
