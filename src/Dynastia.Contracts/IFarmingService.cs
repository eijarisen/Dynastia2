namespace Dynastia.Contracts;

public interface IFarmingService
{
    decimal PurchasePrice { get; }
    decimal SalePrice { get; }
    decimal LivestockPurchasePrice { get; }
    decimal LivestockSalePrice { get; }

    FarmingHouseholdSnapshot GetSnapshot(
        IPerson householdRepresentative);

    bool IsAvailableFarmWorker(
        IPerson person,
        IPerson householdRepresentative);

    bool IsWorkingFarmWorker(
        IPerson person,
        IPerson householdRepresentative);

    decimal GetExpectedAnnualIncome(
        IPerson householdRepresentative);

    decimal GetExpectedAnnualIncomeAfterAddingLocalParcel(
        IPerson householdRepresentative);

    IReadOnlyList<FarmingFlavorInfo> GetAvailableLivestockOptions(
        TownInfo town,
        int year);

    decimal GetFarmlandSaleValue(
        FarmlandAssetInfo farmland);

    FarmlandAssetInfo? AssignNewFarmlandType(
        IPerson householdRepresentative,
        Guid farmlandId);

    FarmlandAssetInfo? EnsureFarmlandFlavor(
        IPerson householdRepresentative,
        Guid farmlandId);

    FarmlandAssetInfo? AddLivestock(
        IPerson householdRepresentative,
        Guid farmlandId,
        int year);

    FarmlandRelocationSaleResult SellOriginFarmlandForVoluntaryRelocation(
        IPerson householdRepresentative,
        TownInfo origin,
        TownInfo destination);
}
