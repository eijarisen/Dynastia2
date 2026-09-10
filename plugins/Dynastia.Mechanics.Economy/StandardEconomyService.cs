using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class StandardEconomyService :
    IEconomyService
{
    private readonly IFamilyService _family;

    public StandardEconomyService(
        IFamilyService family)
    {
        _family = family;
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

        var claim =
            GetClaim(
                person);

        return new HouseholdFinanceSnapshot(
            household.Wealth,
            household.HousesOwned,
            household.RentedHouses,
            claim.PendingInheritance,
            claim.PendingHouses,
            household.NannyId,
            household.LastIncome,
            household.LastExpenses);
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
            household.Wealth + amount);
    }

    public void SetHousesOwned(
        IPerson person,
        int housesOwned)
    {
        var household =
            GetRequiredHousehold(
                person);

        household.HousesOwned =
            Math.Max(
                0,
                housesOwned);

        household.RentedHouses =
            Math.Clamp(
                household.RentedHouses,
                0,
                Math.Max(
                    0,
                    household.HousesOwned - 1));
    }

    public void SetRentedHouses(
        IPerson person,
        int rentedHouses)
    {
        var household =
            GetRequiredHousehold(
                person);

        var maximum =
            Math.Max(
                0,
                household.HousesOwned - 1);

        household.RentedHouses =
            Math.Clamp(
                rentedHouses,
                0,
                maximum);
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
        return GetClaim(
            person)
            .PendingHouses;
    }

    public void SetPendingHouses(
        IPerson person,
        int houses)
    {
        GetClaim(
            person)
            .PendingHouses =
                Math.Max(
                    0,
                    houses);
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

    public void SetNanny(
        IPerson person,
        Guid? nannyId)
    {
        GetRequiredHousehold(
            person)
            .NannyId =
                nannyId;
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
