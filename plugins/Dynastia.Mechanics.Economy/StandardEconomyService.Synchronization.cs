using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    internal void SynchronizeForFinance(
        IPerson person,
        HouseholdEconomyComponent household)
    {
        SynchronizeHouses(
            GetHead(
                household),
            household);
    }

    internal void ReconcileState()
    {
        foreach (var person in _gameState.People)
        {
            var household =
                person.Components.Get<HouseholdEconomyComponent>();

            if (household is not null)
            {
                MigrateHousehold(person, household);
                SynchronizeHouses(GetHead(household), household);
                NormalizeFarmland(household);
            }

            var claim =
                person.Components.Get<PersonalEstateComponent>();

            if (claim is null)
                continue;

            claim.PendingInheritance =
                RoundCurrency(claim.PendingInheritance);

            SynchronizePendingHouses(person, claim);
            NormalizePendingFarmland(claim);
        }
    }

    private void CreateHousehold(
        IPerson head,
        IPerson dynastyAnchor)
    {
        var component =
            new HouseholdEconomyComponent
            {
                HouseholdId =
                    _random.NextGuid(),

                HeadId =
                    head.Id,

                DynastyAnchorId =
                    dynastyAnchor.Id,

                DynastyGeneration =
                    _family.GetGeneration(
                        dynastyAnchor),

                LegacyMembershipSeeded =
                    true,

                ResidenceTownId =
                    _locations.GetLocation(head).HomeTown.Id
            };

        component.MemberIds.Add(
            head.Id);

        if (dynastyAnchor.Id
            != head.Id)
        {
            component.MemberIds.Add(
                dynastyAnchor.Id);
        }

        head.Components.Set(
            component);
    }

    private (
        IPerson Owner,
        HouseholdEconomyComponent Household)?
        FindHousehold(
            IPerson person)
    {
        var direct =
            person.Components.Get<
                HouseholdEconomyComponent>();

        if (direct is not null)
        {
            return (
                person,
                direct);
        }

        foreach (var candidate in
            _gameState.People)
        {
            var household =
                candidate.Components.Get<
                    HouseholdEconomyComponent>();

            if (household is null)
                continue;

            if (household.MemberIds.Contains(
                person.Id))
            {
                return (
                    candidate,
                    household);
            }
        }

        return null;
    }

    private void MigrateHousehold(
        IPerson owner,
        HouseholdEconomyComponent household)
    {
        household.Wealth =
            RoundCurrency(household.Wealth);

        household.LastIncome =
            RoundCurrency(household.LastIncome);

        household.LastExpenses =
            RoundCurrency(household.LastExpenses);

        foreach (var line in household.LastIncomeBreakdown)
            line.Amount = RoundCurrency(line.Amount);

        foreach (var line in household.LastExpenseBreakdown)
            line.Amount = RoundCurrency(line.Amount);

        if (household.HouseholdId
            == Guid.Empty)
        {
            household.HouseholdId =
                _random.NextGuid();
        }

        if (household.HeadId
            == Guid.Empty)
        {
            household.HeadId =
                owner.Id;
        }

        if (household.DynastyAnchorId
            == Guid.Empty)
        {
            var spouse =
                _family.GetSpouse(
                    owner);

            household.DynastyAnchorId =
                _family.IsBloodline(
                    owner)
                    ? owner.Id
                    : spouse is not null
                      && _family.IsBloodline(
                          spouse)
                        ? spouse.Id
                        : owner.Id;
        }

        if (household.DynastyGeneration
            is null)
        {
            var anchor =
                _gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id
                            == household.DynastyAnchorId);

            household.DynastyGeneration =
                anchor is null
                    ? _family.GetGeneration(
                        owner)
                    : _family.GetGeneration(
                        anchor);
        }

        if (!household.MemberIds.Contains(
            household.HeadId))
        {
            household.MemberIds.Insert(
                0,
                household.HeadId);
        }

        if (string.IsNullOrWhiteSpace(household.ResidenceTownId))
        {
            household.ResidenceTownId =
                _locations.GetLocation(GetHead(household)).HomeTown.Id;
        }
    }

    private IPerson GetHead(
        HouseholdEconomyComponent household)
    {
        return _gameState.People
            .FirstOrDefault(
                person =>
                    person.Id
                    == household.HeadId)
            ?? throw new InvalidOperationException(
                "Household head is unavailable.");
    }

    private void SynchronizeHouses(
        IPerson head,
        HouseholdEconomyComponent household)
    {
        if (household.Houses.Count == 0
            && household.HousesOwned > 0)
        {
            for (var index = 0;
                index < household.HousesOwned;
                index++)
            {
                var town =
                    index == 0
                        ? _locations
                            .GetLocation(
                                head)
                            .HomeTown
                        : _locations
                            .ChoosePropertyTown(
                                head);

                household.Houses.Add(
                    new HousePropertyState
                    {
                        Id =
                            _random.NextGuid(),

                        Town =
                            town
                    });
            }
        }

        foreach (var house in
            household.Houses)
        {
            house.Id =
                house.Id == Guid.Empty
                    ? _random.NextGuid()
                    : house.Id;

            house.Town ??=
                _locations
                    .GetLocation(
                        head)
                    .HomeTown;

            if (house.AssignedHeirId is Guid assignedHeirId)
            {
                var assignedHeir =
                    _gameState.People
                        .FirstOrDefault(person =>
                            person.Id == assignedHeirId);

                if (assignedHeir is null
                    || !assignedHeir.Tags.Has("state.alive"))
                {
                    house.AssignedHeirId = null;
                }
            }
        }

        SynchronizeDerivedHouseCounts(
            household);
    }

    private void SynchronizePendingHouses(
        IPerson person,
        PersonalEstateComponent claim)
    {
        if (claim.PendingHouseProperties.Count == 0
            && claim.PendingHouses > 0)
        {
            for (var index = 0;
                index < claim.PendingHouses;
                index++)
            {
                claim.PendingHouseProperties.Add(
                    CreatePendingHouse(
                        person,
                        index));
            }
        }

        foreach (var house in
            claim.PendingHouseProperties)
        {
            house.Id =
                house.Id == Guid.Empty
                    ? _random.NextGuid()
                    : house.Id;

            house.Town ??=
                _locations
                    .GetLocation(
                        person)
                    .HomeTown;
        }

        claim.PendingHouses =
            claim.PendingHouseProperties.Count;
    }

    private HousePropertyState CreatePendingHouse(
        IPerson person,
        int index)
    {
        var town =
            index == 0
                ? _locations
                    .GetLocation(
                        person)
                    .HomeTown
                : _locations
                    .ChoosePropertyTown(
                        person);

        return new HousePropertyState
        {
            Id =
                _random.NextGuid(),

            Town =
                town
        };
    }

    private void SynchronizeDerivedHouseCounts(
        HouseholdEconomyComponent household)
    {
        household.HousesOwned = household.Houses.Count;

        if (household.Houses.Count == 0)
        {
            household.RentedHouses = 0;
            return;
        }

        var head = GetHead(household);
        var homeTown = _locations.GetLocation(head).HomeTown;
        var hasResidence = household.Houses.Any(house =>
            house.Town is not null
            && house.Town.Id.Equals(homeTown.Id, StringComparison.OrdinalIgnoreCase));

        household.RentedHouses = household.Houses.Count - (hasResidence ? 1 : 0);
    }

    private HousePropertyInfo ToInfo(
        HousePropertyState house,
        int index,
        IPerson head,
        HouseholdEconomyComponent household)
    {
        var town = house.Town
            ?? throw new InvalidOperationException("House town is unavailable.");
        var homeTown = _locations.GetLocation(head).HomeTown;
        var firstLocalIndex = household.Houses.FindIndex(candidate =>
            candidate.Town is not null
            && candidate.Town.Id.Equals(homeTown.Id, StringComparison.OrdinalIgnoreCase));
        var isResidence = index == firstLocalIndex;

        return new HousePropertyInfo(
            house.Id,
            town,
            IsResidence: isResidence,
            IsRented: !isResidence,
            AssignedHeirId: house.AssignedHeirId);
    }

    private static HousePropertyInfo ToInfo(
        HousePropertyState house,
        int index)
    {
        var town =
            house.Town
            ?? throw new InvalidOperationException(
                "House town is unavailable.");

        return new HousePropertyInfo(
            house.Id,
            town,
            IsResidence:
                index == 0,
            IsRented:
                index > 0,
            AssignedHeirId:
                house.AssignedHeirId);
    }

    private static PersonalEstateComponent?
        FindClaim(
            IPerson person) =>
        person.Components.Get<PersonalEstateComponent>();

    private static PersonalEstateComponent
        GetClaim(
            IPerson person)
    {
        var claim =
            person.Components.Get<
                PersonalEstateComponent>();

        if (claim is not null)
        {
            claim.PendingInheritance =
                RoundCurrency(
                    claim.PendingInheritance);

            return claim;
        }

        claim =
            new PersonalEstateComponent();

        person.Components.Set(
            claim);

        return claim;
    }
}
