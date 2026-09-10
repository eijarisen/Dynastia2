using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class StandardEconomyService :
    IEconomyService
{
    private readonly IFamilyService _family;
    private readonly ILocationService _locations;

    public StandardEconomyService(
        IFamilyService family,
        ILocationService locations)
    {
        _family = family;
        _locations = locations;
    }

    public bool HasHousehold(
        IPerson person)
    {
        return person.Components.Has<
            HouseholdEconomyComponent>();
    }

    public void EnsureHousehold(
        IPerson person)
    {
        if (HasHousehold(person))
            return;

        if (_family.GetSex(person)
                != Sex.Male
            || !_family.IsMaleLineage(
                person))
        {
            return;
        }

        person.Components.Set(
            new HouseholdEconomyComponent());
    }

    public void EnsureIndependentHousehold(
        IPerson person)
    {
        if (HasHousehold(person))
            return;

        person.Components.Set(
            new HouseholdEconomyComponent());
    }

    public HouseholdFinanceSnapshot?
        GetHousehold(
            IPerson person)
    {
        EnsureHousehold(
            person);

        var household =
            person.Components.Get<
                HouseholdEconomyComponent>();

        if (household is null)
            return null;

        SynchronizeHouses(
            person,
            household);

        var claim =
            GetClaim(
                person);

        SynchronizePendingHouses(
            person,
            claim);

        var houses =
            household.Houses
                .Select(
                    (house, index) =>
                        ToInfo(
                            house,
                            index))
                .ToList();

        return new HouseholdFinanceSnapshot(
            household.Wealth,
            houses.Count,
            Math.Max(
                0,
                houses.Count - 1),
            claim.PendingInheritance,
            claim.PendingHouseProperties.Count,
            household.NannyId,
            household.LastIncome,
            household.LastExpenses,
            household.LastIncomeBreakdown
                .Select(
                    line =>
                        new FinanceBreakdownItem(
                            line.Label,
                            line.Amount))
                .ToList(),
            household.LastExpenseBreakdown
                .Select(
                    line =>
                        new FinanceBreakdownItem(
                            line.Label,
                            line.Amount))
                .ToList(),
            houses);
    }

    public void SetWealth(
        IPerson person,
        decimal wealth)
    {
        var household =
            GetRequiredHousehold(
                person);

        household.Wealth =
            Math.Max(
                0,
                wealth);
    }

    public void ChangeWealth(
        IPerson person,
        decimal amount)
    {
        var household =
            GetRequiredHousehold(
                person);

        SetWealth(
            person,
            household.Wealth
            + amount);
    }

    public void SetHousesOwned(
        IPerson person,
        int housesOwned)
    {
        var household =
            GetRequiredHousehold(
                person);

        SynchronizeHouses(
            person,
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
        // Rental status is now automatic:
        // first house = residence; every additional house = rented.
        // Keep this method for source/API compatibility, but ignore the
        // requested number and normalize derived state instead.
        var household =
            GetRequiredHousehold(
                person);

        SynchronizeHouses(
            person,
            household);

        SynchronizeDerivedHouseCounts(
            household);
    }

    public IReadOnlyList<HousePropertyInfo> GetHouses(
        IPerson person)
    {
        var household =
            GetRequiredHousehold(
                person);

        SynchronizeHouses(
            person,
            household);

        return household.Houses
            .Select(
                (house, index) =>
                    ToInfo(
                        house,
                        index))
            .ToList();
    }

    public HousePropertyInfo AddHouse(
        IPerson person,
        TownInfo? town = null)
    {
        var household =
            GetRequiredHousehold(
                person);

        SynchronizeHouses(
            person,
            household);

        var firstHouse =
            household.Houses.Count == 0;

        var assignedTown =
            town
            ?? (
                firstHouse
                    ? _locations
                        .GetLocation(
                            person)
                        .HomeTown
                    : _locations
                        .ChoosePropertyTown(
                            person)
            );

        var state =
            new HousePropertyState
            {
                Id =
                    Guid.NewGuid(),

                Town =
                    assignedTown
            };

        household.Houses.Add(
            state);

        if (firstHouse)
        {
            _locations.SetHouseholdHomeTown(
                person,
                assignedTown);
        }

        SynchronizeDerivedHouseCounts(
            household);

        return ToInfo(
            state,
            household.Houses.Count - 1);
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

        SynchronizeHouses(
            person,
            household);

        if (household.Houses.Any(
            existing =>
                existing.Id
                == house.Id))
        {
            return;
        }

        var firstHouse =
            household.Houses.Count == 0;

        household.Houses.Add(
            new HousePropertyState
            {
                Id =
                    house.Id,

                Town =
                    house.Town
            });

        if (firstHouse)
        {
            _locations.SetHouseholdHomeTown(
                person,
                house.Town);
        }

        SynchronizeDerivedHouseCounts(
            household);
    }

    public HousePropertyInfo? TakeAdditionalHouse(
        IPerson person)
    {
        var household =
            GetRequiredHousehold(
                person);

        SynchronizeHouses(
            person,
            household);

        if (household.Houses.Count <= 1)
            return null;

        var index =
            household.Houses.Count - 1;

        var state =
            household.Houses[index];

        var info =
            ToInfo(
                state,
                index);

        household.Houses.RemoveAt(
            index);

        SynchronizeDerivedHouseCounts(
            household);

        return info;
    }

    public IReadOnlyList<HousePropertyInfo> TakeAllHouses(
        IPerson person)
    {
        var household =
            GetRequiredHousehold(
                person);

        SynchronizeHouses(
            person,
            household);

        var houses =
            household.Houses
                .Select(
                    (house, index) =>
                        ToInfo(
                            house,
                            index))
                .ToList();

        household.Houses.Clear();

        SynchronizeDerivedHouseCounts(
            household);

        return houses;
    }

    public decimal GetPendingInheritance(
        IPerson person)
    {
        return GetClaim(
            person)
            .PendingInheritance;
    }

    public void SetPendingInheritance(
        IPerson person,
        decimal amount)
    {
        GetClaim(
            person)
            .PendingInheritance =
                Math.Max(
                    0,
                    amount);
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
        var claim =
            GetClaim(
                person);

        SynchronizePendingHouses(
            person,
            claim);

        return claim
            .PendingHouseProperties
            .Count;
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

                    Town =
                        house.Town
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
        claim.PendingHouses = 0;

        return result;
    }

    public void SetNanny(
        IPerson person,
        Guid? nannyId)
    {
        GetRequiredHousehold(
            person)
            .NannyId =
                nannyId;
    }

    public IReadOnlyList<Guid> GetHostedDependentIds(
        IPerson householdHead)
    {
        var household =
            GetRequiredHousehold(
                householdHead);

        return household
            .HostedDependentIds
            .ToList();
    }

    public void AddHostedDependent(
        IPerson householdHead,
        IPerson dependent)
    {
        var household =
            GetRequiredHousehold(
                householdHead);

        if (!household.HostedDependentIds.Contains(
            dependent.Id))
        {
            household.HostedDependentIds.Add(
                dependent.Id);
        }
    }

    public void RemoveHostedDependent(
        IPerson householdHead,
        IPerson dependent)
    {
        if (!HasHousehold(
            householdHead))
        {
            return;
        }

        GetRequiredHousehold(
            householdHead)
            .HostedDependentIds
            .Remove(
                dependent.Id);
    }

    internal HouseholdEconomyComponent
        GetRequiredHousehold(
            IPerson person)
    {
        EnsureHousehold(
            person);

        return person.Components.Get<
            HouseholdEconomyComponent>()
            ?? throw new InvalidOperationException(
                $"{_family.GetDisplayName(person)} " +
                "does not own a dynasty household.");
    }

    internal void SynchronizeForFinance(
        IPerson person,
        HouseholdEconomyComponent household)
    {
        SynchronizeHouses(
            person,
            household);
    }

    private void SynchronizeHouses(
        IPerson person,
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
                                person)
                            .HomeTown
                        : _locations
                            .ChoosePropertyTown(
                                person);

                household.Houses.Add(
                    new HousePropertyState
                    {
                        Id =
                            Guid.NewGuid(),

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
                    ? Guid.NewGuid()
                    : house.Id;

            house.Town ??=
                _locations
                    .GetLocation(
                        person)
                    .HomeTown;
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
                    ? Guid.NewGuid()
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
                Guid.NewGuid(),

            Town =
                town
        };
    }

    private static void SynchronizeDerivedHouseCounts(
        HouseholdEconomyComponent household)
    {
        household.HousesOwned =
            household.Houses.Count;

        household.RentedHouses =
            Math.Max(
                0,
                household.Houses.Count
                - 1);
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
                index > 0);
    }

    private static PersonalEstateComponent
        GetClaim(
            IPerson person)
    {
        var claim =
            person.Components.Get<
                PersonalEstateComponent>();

        if (claim is not null)
            return claim;

        claim =
            new PersonalEstateComponent();

        person.Components.Set(
            claim);

        return claim;
    }
}
