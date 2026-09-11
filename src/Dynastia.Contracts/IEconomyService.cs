namespace Dynastia.Contracts;

public interface IEconomyService
{
    bool HasHousehold(IPerson person);

    void EnsureHousehold(IPerson person);

    void EnsureIndependentHousehold(
        IPerson person,
        IPerson? dynastyAnchor = null);

    HouseholdFinanceSnapshot? GetHousehold(
        IPerson person);

    Guid? GetHouseholdId(
        IPerson person);

    Guid? GetHouseholdDynastyAnchorId(
        IPerson person);

    IReadOnlyList<Guid> GetHouseholdMemberIds(
        IPerson person);

    bool IsLegacyMembershipSeeded(
        IPerson householdRepresentative);

    void MarkLegacyMembershipSeeded(
        IPerson householdRepresentative);

    void AddHouseholdMember(
        IPerson householdRepresentative,
        IPerson member);

    void RemoveHouseholdMember(
        IPerson member);

    void TransferHouseholdHead(
        IPerson currentHead,
        IPerson newHead);

    void MarkEstateReady(
        IPerson householdRepresentative,
        bool ready = true);

    bool IsEstateReady(
        IPerson householdRepresentative);

    void DissolveHousehold(
        IPerson householdRepresentative);

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

    IReadOnlyList<HousePropertyInfo> GetHouses(
        IPerson person);

    HousePropertyInfo AddHouse(
        IPerson person,
        TownInfo? town = null);

    void AddExistingHouse(
        IPerson person,
        HousePropertyInfo house);

    HousePropertyInfo? TakeAdditionalHouse(
        IPerson person);

    IReadOnlyList<HousePropertyInfo> TakeAllHouses(
        IPerson person);

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

    void AddPendingHouse(
        IPerson person,
        HousePropertyInfo house);

    IReadOnlyList<HousePropertyInfo> TakePendingHouses(
        IPerson person);

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
