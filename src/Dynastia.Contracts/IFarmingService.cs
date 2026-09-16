namespace Dynastia.Contracts;

public interface IFarmingService
{
    decimal PurchasePrice { get; }
    decimal SalePrice { get; }

    FarmingHouseholdSnapshot GetSnapshot(
        IPerson householdRepresentative);

    bool IsAvailableFarmWorker(
        IPerson person,
        IPerson householdRepresentative);

    decimal GetExpectedAnnualIncome(
        IPerson householdRepresentative);
}
