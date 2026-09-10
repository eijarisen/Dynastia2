namespace Dynastia.Contracts;

public interface IEconomyService
{
    bool HasHousehold(IPerson person);

    void EnsureHousehold(IPerson person);

    void EnsureIndependentHousehold(
        IPerson person);

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

    decimal GetPendingInheritance(
        IPerson person);

    void SetPendingInheritance(
        IPerson person,
        decimal amount);

    void ChangePendingInheritance(
        IPerson person,
        decimal amount);

    int GetPendingHouses(
        IPerson person);

    void SetPendingHouses(
        IPerson person,
        int houses);

    void ChangePendingHouses(
        IPerson person,
        int houses);

    void SetNanny(
        IPerson person,
        Guid? nannyId);

    IReadOnlyList<Guid> GetHostedDependentIds(
        IPerson householdHead);

    void AddHostedDependent(
        IPerson householdHead,
        IPerson dependent);

    void RemoveHostedDependent(
        IPerson householdHead,
        IPerson dependent);
}
