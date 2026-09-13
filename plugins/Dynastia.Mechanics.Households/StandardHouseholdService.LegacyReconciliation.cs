using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed partial class StandardHouseholdService
{
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

}
