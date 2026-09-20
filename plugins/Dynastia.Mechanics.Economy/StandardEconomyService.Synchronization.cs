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
                SynchronizeLifestyleTags(household);
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

        for (var index = 0; index < household.BudgetHistory.Count; index++)
        {
            var point = household.BudgetHistory[index];
            household.BudgetHistory[index] = point with
            {
                Wealth = RoundCurrency(point.Wealth),
                Income = RoundCurrency(point.Income),
                Expenses = RoundCurrency(point.Expenses)
            };
        }

        if (household.BudgetHistory.Count == 0)
            RecordBudgetHistory(household, _gameState.Year);

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

                        TownId =
                            town.Id,

                        PurchasePrice =
                            GetHousePrice(town)
                    });
            }
        }

        var fallbackTownId =
            _locations
                .GetLocation(
                    head)
                .HomeTown
                .Id;

        foreach (var house in
            household.Houses)
        {
            house.Id =
                house.Id == Guid.Empty
                    ? _random.NextGuid()
                    : house.Id;

            if (string.IsNullOrWhiteSpace(house.TownId)
                || _locations.FindTown(house.TownId) is null)
            {
                house.TownId = fallbackTownId;
            }

            var houseTown = ResolveHouseTown(house);
            if (house.PurchasePrice <= 0m)
                house.PurchasePrice = GetHousePrice(houseTown);

            house.CapacityExtensions = Math.Max(0, house.CapacityExtensions);

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

        var fallbackTownId =
            _locations
                .GetLocation(
                    person)
                .HomeTown
                .Id;

        foreach (var house in
            claim.PendingHouseProperties)
        {
            house.Id =
                house.Id == Guid.Empty
                    ? _random.NextGuid()
                    : house.Id;

            if (string.IsNullOrWhiteSpace(house.TownId)
                || _locations.FindTown(house.TownId) is null)
            {
                house.TownId = fallbackTownId;
            }

            var pendingTown = ResolveHouseTown(house);
            if (house.PurchasePrice <= 0m)
                house.PurchasePrice = GetHousePrice(pendingTown);

            house.CapacityExtensions = Math.Max(0, house.CapacityExtensions);
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

            TownId =
                town.Id,

            PurchasePrice =
                GetHousePrice(town)
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
        var homeTownId = _locations.GetLocation(head).HomeTown.Id;
        var hasResidence = household.Houses.Any(house =>
            !string.IsNullOrWhiteSpace(house.TownId)
            && house.TownId.Equals(
                homeTownId,
                StringComparison.OrdinalIgnoreCase));

        household.RentedHouses = household.Houses.Count - (hasResidence ? 1 : 0);
    }

    private HousePropertyInfo ToInfo(
        HousePropertyState house,
        int index,
        IPerson head,
        HouseholdEconomyComponent household)
    {
        var town = ResolveHouseTown(house);
        var homeTownId = _locations.GetLocation(head).HomeTown.Id;
        var firstLocalIndex = household.Houses.FindIndex(candidate =>
            !string.IsNullOrWhiteSpace(candidate.TownId)
            && candidate.TownId.Equals(
                homeTownId,
                StringComparison.OrdinalIgnoreCase));
        var isResidence = index == firstLocalIndex;

        return new HousePropertyInfo(
            house.Id,
            town,
            IsResidence: isResidence,
            IsRented: !isResidence,
            AssignedHeirId: house.AssignedHeirId,
            PurchasePrice: house.PurchasePrice,
            CapacityExtensions: house.CapacityExtensions);
    }

    private HousePropertyInfo ToInfo(
        HousePropertyState house,
        int index)
    {
        var town = ResolveHouseTown(house);

        return new HousePropertyInfo(
            house.Id,
            town,
            IsResidence:
                index == 0,
            IsRented:
                index > 0,
            AssignedHeirId:
                house.AssignedHeirId,
            PurchasePrice:
                house.PurchasePrice,
            CapacityExtensions:
                house.CapacityExtensions);
    }

    private TownInfo ResolveHouseTown(
        HousePropertyState house)
    {
        return _locations.FindTown(house.TownId)
            ?? throw new InvalidOperationException(
                $"House town '{house.TownId}' is unavailable.");
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
