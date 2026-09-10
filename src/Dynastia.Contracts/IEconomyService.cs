namespace Dynastia.Contracts;

public interface IEconomyService
{
    bool HasHousehold(IPerson person);

    void EnsureHousehold(IPerson person);

    HouseholdFinanceSnapshot? GetHousehold(
        IPerson person);

    void SetWealth(
        IPerson person,
        decimal wealth);

    void ChangeWealth(
        IPerson person,
        decimal amount);

    void SetHousesOwned(
        IPerson person,
        int housesOwned);

    void SetRentedHouses(
        IPerson person,
        int rentedHouses);

    void SetPendingInheritance(
        IPerson person,
        decimal amount);

    void SetPendingHouses(
        IPerson person,
        int houses);

    void SetNanny(
        IPerson person,
        Guid? nannyId);
}
