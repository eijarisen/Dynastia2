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

        var component =
            person.Components.Get<
                HouseholdEconomyComponent>();

        if (component is null)
            return null;

        return new HouseholdFinanceSnapshot(
            component.Wealth,
            component.HousesOwned,
            component.RentedHouses,
            component.PendingInheritance,
            component.PendingHouses,
            component.NannyId,
            component.LastIncome,
            component.LastExpenses);
    }

    public void SetWealth(
        IPerson person,
        decimal wealth)
    {
        var component =
            GetRequired(
                person);

        component.Wealth =
            Math.Max(
                0,
                wealth);
    }

    public void ChangeWealth(
        IPerson person,
        decimal amount)
    {
        var component =
            GetRequired(
                person);

        SetWealth(
            person,
            component.Wealth + amount);
    }

    public void SetHousesOwned(
        IPerson person,
        int housesOwned)
    {
        var component =
            GetRequired(
                person);

        component.HousesOwned =
            Math.Max(
                0,
                housesOwned);

        component.RentedHouses =
            Math.Clamp(
                component.RentedHouses,
                0,
                Math.Max(
                    0,
                    component.HousesOwned - 1));
    }

    public void SetRentedHouses(
        IPerson person,
        int rentedHouses)
    {
        var component =
            GetRequired(
                person);

        var maximum =
            Math.Max(
                0,
                component.HousesOwned - 1);

        component.RentedHouses =
            Math.Clamp(
                rentedHouses,
                0,
                maximum);
    }

    public void SetPendingInheritance(
        IPerson person,
        decimal amount)
    {
        GetRequired(
            person)
            .PendingInheritance =
                Math.Max(
                    0,
                    amount);
    }

    public void SetPendingHouses(
        IPerson person,
        int houses)
    {
        GetRequired(
            person)
            .PendingHouses =
                Math.Max(
                    0,
                    houses);
    }

    public void SetNanny(
        IPerson person,
        Guid? nannyId)
    {
        GetRequired(
            person)
            .NannyId =
                nannyId;
    }

    internal HouseholdEconomyComponent
        GetRequired(
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
}
