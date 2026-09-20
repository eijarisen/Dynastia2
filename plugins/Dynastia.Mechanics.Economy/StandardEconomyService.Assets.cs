using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    public TownInfo GetResidenceTown(
        IPerson person)
    {
        var resolved = FindHousehold(person)
            ?? throw new InvalidOperationException(
                $"{_family.GetDisplayName(person)} does not belong to an active dynasty household.");

        var household = resolved.Household;
        var head = GetHead(household);

        var town =
            !string.IsNullOrWhiteSpace(
                household.ResidenceTownId)
                ? _locations.FindTown(
                    household.ResidenceTownId)
                : null;

        return town
            ?? _locations.GetLocation(head).HomeTown;
    }

    public void SetResidenceTown(
        IPerson person,
        TownInfo town)
    {
        ArgumentNullException.ThrowIfNull(town);

        var household =
            GetRequiredHousehold(
                person);

        household.ResidenceTownId =
            town.Id;

        SetMemberHomeTowns(
            household,
            town);

        SynchronizeDerivedHouseCounts(
            household);
    }

    private void SetMemberHomeTowns(
        HouseholdEconomyComponent household,
        TownInfo town)
    {
        var memberIds =
            household.MemberIds
                .Append(
                    household.HeadId)
                .Distinct()
                .ToList();

        foreach (var memberId in
            memberIds)
        {
            var member =
                _gameState.People
                    .FirstOrDefault(
                        candidate =>
                            candidate.Id
                            == memberId);

            if (member is null
                || member.Tags.Has(
                    "state.dead"))
            {
                continue;
            }

            _locations.SetPersonHomeTown(
                member,
                town);
        }
    }

    public void SetWealth(
        IPerson person,
        decimal wealth)
    {
        var household =
            GetRequiredHousehold(
                person);

        household.Wealth =
            RoundCurrency(
                Math.Max(
                    0,
                    wealth));
    }

    public void ChangeWealth(
        IPerson person,
        decimal amount)
    {
        var household =
            GetRequiredHousehold(
                person);

        household.Wealth =
            RoundCurrency(
                EconomyBalanceRules.ApplyOrdinaryWealthChange(
                    household.Wealth,
                    RoundCurrency(amount)));
    }

    public void ChangeWealthAllowDebt(
        IPerson person,
        decimal amount)
    {
        var household =
            GetRequiredHousehold(
                person);

        household.Wealth =
            RoundCurrency(
                household.Wealth
                + RoundCurrency(amount));
    }

    public void SetHousesOwned(
        IPerson person,
        int housesOwned)
    {
        var household =
            GetRequiredHousehold(
                person);

        var owner =
            GetHead(
                household);

        SynchronizeHouses(
            owner,
            household);

        var target =
            Math.Max(
                0,
                housesOwned);

        while (household.Houses.Count
            < target)
        {
            AddHouse(
                person);
        }

        while (household.Houses.Count
            > target)
        {
            household.Houses.RemoveAt(
                household.Houses.Count
                - 1);
        }

        SynchronizeDerivedHouseCounts(
            household);
    }

    public void SetRentedHouses(
        IPerson person,
        int rentedHouses)
    {
        var household =
            GetRequiredHousehold(
                person);

        SynchronizeDerivedHouseCounts(
            household);
    }

    public IReadOnlyList<HousePropertyInfo> GetHouses(
        IPerson person)
    {
        var resolved = FindHousehold(person);
        if (resolved is null)
            return Array.Empty<HousePropertyInfo>();

        var household = resolved.Value.Household;

        return household.Houses
            .Select(
                (house, index) =>
                    ToInfo(
                        house,
                        index,
                        GetHead(household),
                        household))
            .ToList();
    }

    public HousePropertyInfo AddHouse(
        IPerson person,
        TownInfo? town = null)
    {
        var household =
            GetRequiredHousehold(
                person);

        var head =
            GetHead(
                household);

        SynchronizeHouses(
            head,
            household);

        var firstHouse =
            household.Houses.Count == 0;

        var assignedTown =
            town
            ?? (
                firstHouse
                    ? _locations
                        .GetLocation(
                            head)
                        .HomeTown
                    : _locations
                        .ChoosePropertyTown(
                            head)
            );

        var state =
            new HousePropertyState
            {
                Id =
                    _random.NextGuid(),

                TownId =
                    assignedTown.Id,

                PurchasePrice =
                    GetHousePrice(assignedTown)
            };

        household.Houses.Add(
            state);

        SynchronizeDerivedHouseCounts(
            household);

        return ToInfo(
            state,
            household.Houses.Count - 1,
            head,
            household);
    }

    public void AddExistingHouse(
        IPerson person,
        HousePropertyInfo house)
    {
        ArgumentNullException.ThrowIfNull(
            house);

        var household =
            GetRequiredHousehold(
                person);

        var head =
            GetHead(
                household);

        SynchronizeHouses(
            head,
            household);

        if (household.Houses.Any(
            existing =>
                existing.Id
                == house.Id))
        {
            return;
        }

        household.Houses.Add(
            new HousePropertyState
            {
                Id =
                    house.Id,

                TownId =
                    house.Town.Id,

                AssignedHeirId =
                    house.AssignedHeirId,

                PurchasePrice =
                    house.PurchasePrice > 0m
                        ? house.PurchasePrice
                        : GetHousePrice(house.Town),

                CapacityExtensions =
                    Math.Max(0, house.CapacityExtensions)
            });

        SynchronizeDerivedHouseCounts(
            household);
    }

    public HousePropertyInfo? TakeAdditionalHouse(
        IPerson person)
    {
        var household = GetRequiredHousehold(person);
        var head = GetHead(household);
        SynchronizeHouses(head, household);

        var houses = household.Houses
            .Select((house, index) => new { House = house, Index = index, Info = ToInfo(house, index, head, household) })
            .Where(item => item.Info.IsRented)
            .ToList();

        if (houses.Count == 0)
            return null;

        var selected = houses[^1];
        household.Houses.RemoveAt(selected.Index);
        SynchronizeDerivedHouseCounts(household);
        return selected.Info;
    }

    public HousePropertyInfo? TakeHouse(
        IPerson person,
        Guid propertyId)
    {
        var household = GetRequiredHousehold(person);
        var head = GetHead(household);
        SynchronizeHouses(head, household);

        var index = household.Houses.FindIndex(house => house.Id == propertyId);
        if (index < 0)
            return null;

        var state = household.Houses[index];
        var info = ToInfo(state, index, head, household);
        household.Houses.RemoveAt(index);
        SynchronizeDerivedHouseCounts(household);
        return info;
    }

    public bool SetHouseInheritanceHeir(
        IPerson person,
        Guid propertyId,
        Guid? heirId)
    {
        var household = GetRequiredHousehold(person);
        var head = GetHead(household);
        SynchronizeHouses(head, household);

        var house = household.Houses
            .FirstOrDefault(candidate => candidate.Id == propertyId);

        if (house is null)
            return false;

        if (heirId is Guid selectedHeirId)
        {
            var validChild = _family.GetChildren(person)
                .Any(child =>
                    child.Id == selectedHeirId
                    && child.Tags.Has("state.alive"));

            if (!validChild)
                return false;
        }

        house.AssignedHeirId = heirId;
        return true;
    }

    public decimal GetHousePrice(TownInfo town) =>
        RoundCurrency(
            BaseHousePrice * town.HousingIndex);

    public decimal GetHouseSaleValue(TownInfo town) =>
        RoundCurrency(
            GetHousePrice(town) * 0.80m);

    public decimal GetHouseValue(HousePropertyInfo house)
    {
        ArgumentNullException.ThrowIfNull(house);
        var purchasePrice = house.PurchasePrice > 0m
            ? house.PurchasePrice
            : GetHousePrice(house.Town);
        var improvementValue =
            purchasePrice
            * HouseExtensionRules.ExtensionPriceFraction
            * Math.Max(0, house.CapacityExtensions);
        return RoundCurrency(
            GetHousePrice(house.Town) + improvementValue);
    }

    public decimal GetHouseSaleValue(HousePropertyInfo house) =>
        RoundCurrency(
            GetHouseValue(house) * 0.80m);

    public decimal GetLivingCostPerPerson(TownInfo town) =>
        RoundCurrency(
            OrdinaryLivingCostUnit * town.LivingCostIndex);

    public decimal GetResidenceRent(TownInfo town) =>
        GetRentalIncome(town);

    public decimal GetRentalIncome(TownInfo town) =>
        RoundCurrency(
            GetHousePrice(town) / 40m);

    private static decimal RoundCurrency(
        decimal amount) =>
        Math.Round(
            amount,
            0,
            MidpointRounding.AwayFromZero);

    public IReadOnlyList<HousePropertyInfo> TakeAllHouses(
        IPerson person)
    {
        var household =
            GetRequiredHousehold(
                person);

        SynchronizeHouses(
            GetHead(household),
            household);

        var houses =
            household.Houses
                .Select(
                    (house, index) =>
                        ToInfo(
                            house,
                            index,
                            GetHead(household),
                            household))
                .ToList();

        household.Houses.Clear();

        SynchronizeDerivedHouseCounts(
            household);

        return houses;
    }

    public decimal GetPendingInheritance(
        IPerson person)
    {
        return FindClaim(person)?.PendingInheritance
            ?? 0m;
    }

    public void SetPendingInheritance(
        IPerson person,
        decimal amount)
    {
        GetClaim(
            person)
            .PendingInheritance =
                RoundCurrency(
                    Math.Max(
                        0,
                        amount));
    }

    public void ChangePendingInheritance(
        IPerson person,
        decimal amount)
    {
        SetPendingInheritance(
            person,
            GetPendingInheritance(person)
            + amount);
    }

    public int GetPendingHouses(
        IPerson person)
    {
        var claim = FindClaim(person);
        return claim?.PendingHouseProperties.Count
            ?? claim?.PendingHouses
            ?? 0;
    }

    public void SetPendingHouses(
        IPerson person,
        int houses)
    {
        var claim =
            GetClaim(
                person);

        SynchronizePendingHouses(
            person,
            claim);

        var target =
            Math.Max(
                0,
                houses);

        while (claim.PendingHouseProperties.Count
            < target)
        {
            claim.PendingHouseProperties.Add(
                CreatePendingHouse(
                    person,
                    claim.PendingHouseProperties.Count));
        }

        while (claim.PendingHouseProperties.Count
            > target)
        {
            claim.PendingHouseProperties.RemoveAt(
                claim.PendingHouseProperties.Count
                - 1);
        }

        claim.PendingHouses =
            claim.PendingHouseProperties.Count;
    }

    public void ChangePendingHouses(
        IPerson person,
        int houses)
    {
        SetPendingHouses(
            person,
            GetPendingHouses(person)
            + houses);
    }

    public void AddPendingHouse(
        IPerson person,
        HousePropertyInfo house)
    {
        ArgumentNullException.ThrowIfNull(
            house);

        var claim =
            GetClaim(
                person);

        SynchronizePendingHouses(
            person,
            claim);

        if (!claim.PendingHouseProperties.Any(
            existing =>
                existing.Id
                == house.Id))
        {
            claim.PendingHouseProperties.Add(
                new HousePropertyState
                {
                    Id =
                        house.Id,

                    TownId =
                        house.Town.Id,

                    AssignedHeirId =
                        house.AssignedHeirId,

                    PurchasePrice =
                        house.PurchasePrice > 0m
                            ? house.PurchasePrice
                            : GetHousePrice(house.Town),

                    CapacityExtensions =
                        Math.Max(0, house.CapacityExtensions)
                });
        }

        claim.PendingHouses =
            claim.PendingHouseProperties.Count;
    }

    public IReadOnlyList<HousePropertyInfo> TakePendingHouses(
        IPerson person)
    {
        var claim =
            GetClaim(
                person);

        SynchronizePendingHouses(
            person,
            claim);

        var result =
            claim.PendingHouseProperties
                .Select(
                    (house, index) =>
                        ToInfo(
                            house,
                            index))
                .ToList();

        claim.PendingHouseProperties.Clear();
        claim.PendingHouses =
            0;

        return result;
    }

}
