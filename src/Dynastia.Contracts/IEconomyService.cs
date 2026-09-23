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

    bool CanAfford(
        IPerson person,
        decimal amount);

    decimal GetProjectedAnnualIncome(
        IPerson person);

    IReadOnlyList<FinanceBreakdownItem> GetProjectedIncomeBreakdown(
        IPerson person);

    HouseholdAnnualForecast? GetAnnualForecast(
        IPerson person);

    bool HasSufficientPassiveIncomeForBasicNeeds(
        IPerson person) =>
        false;

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

    TownInfo GetResidenceTown(
        IPerson person);

    void SetResidenceTown(
        IPerson person,
        TownInfo town);

    void SetWealth(
        IPerson person,
        decimal wealth);

    void ChangeWealth(
        IPerson person,
        decimal amount);

    // Explicit debt-producing mechanics (for example loan repayment and
    // gambling losses) may make a household balance negative. Ordinary
    // ChangeWealth callers retain the historical zero floor.
    void ChangeWealthAllowDebt(
        IPerson person,
        decimal amount);

    void RecordRealizedExpense(
        IPerson person,
        string label,
        decimal amount);

    void ApplyAnnualFinanceReceipt(
        IPerson recipient,
        string label,
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

    HousePropertyInfo AddHouse(
        IPerson person,
        TownInfo town,
        decimal purchasePrice,
        int baseResidentCapacity) =>
        AddHouse(person, town);

    void AddExistingHouse(
        IPerson person,
        HousePropertyInfo house);

    HousePropertyInfo? TakeAdditionalHouse(
        IPerson person);

    HousePropertyInfo? TakeHouse(
        IPerson person,
        Guid propertyId);

    bool SetHouseInheritanceHeir(
        IPerson person,
        Guid propertyId,
        Guid? heirId);

    decimal GetHousePrice(TownInfo town);

    decimal GetHouseSaleValue(TownInfo town);

    decimal GetHouseValue(HousePropertyInfo house)
    {
        ArgumentNullException.ThrowIfNull(house);
        var purchasePrice = house.PurchasePrice > 0m
            ? house.PurchasePrice
            : GetHousePrice(house.Town);
        var improvementValue =
            Math.Round(
                purchasePrice * 0.25m * Math.Max(0, house.CapacityExtensions),
                0,
                MidpointRounding.AwayFromZero);
        return Math.Round(
            GetHousePrice(house.Town) + improvementValue,
            0,
            MidpointRounding.AwayFromZero);
    }

    decimal GetHouseSaleValue(HousePropertyInfo house)
    {
        ArgumentNullException.ThrowIfNull(house);
        return Math.Round(
            GetHouseValue(house) * 0.80m,
            0,
            MidpointRounding.AwayFromZero);
    }

    decimal GetLivingCostPerPerson(TownInfo town);

    decimal GetResidenceRent(TownInfo town);

    decimal GetRentalIncome(TownInfo town);

    decimal GetRentalIncome(HousePropertyInfo house)
    {
        ArgumentNullException.ThrowIfNull(house);
        return Math.Round(
            GetHouseValue(house) / 40m,
            0,
            MidpointRounding.AwayFromZero);
    }

    IReadOnlyList<HousePropertyInfo> TakeAllHouses(
        IPerson person);

    IReadOnlyList<FarmlandAssetInfo> GetFarmland(
        IPerson person);

    FarmlandAssetInfo AddFarmland(
        IPerson person,
        TownInfo town,
        int acquiredYear,
        string acquisitionSource);

    void AddExistingFarmland(
        IPerson person,
        FarmlandAssetInfo farmland);

    FarmlandAssetInfo? TakeFarmland(
        IPerson person,
        Guid farmlandId);

    bool SetFarmlandInheritanceHeir(
        IPerson person,
        Guid farmlandId,
        Guid? heirId);

    bool SetFarmlandFlavor(
        IPerson person,
        Guid farmlandId,
        string farmTypeId,
        string? livestockTypeId) => false;

    IReadOnlyList<FarmlandAssetInfo> TakeAllFarmland(
        IPerson person);

    // Signed pending estate balance: positive inheritance, negative inherited debt.
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

    IReadOnlyList<FarmlandAssetInfo> GetPendingFarmland(
        IPerson person);

    void AddPendingFarmland(
        IPerson person,
        FarmlandAssetInfo farmland);

    IReadOnlyList<FarmlandAssetInfo> TakePendingFarmland(
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
