namespace Dynastia.Contracts;

public sealed record HouseholdFinanceSnapshot(
    decimal Wealth,
    int HousesOwned,
    int RentedHouses,
    decimal PendingInheritance,
    int PendingHouses,
    Guid? NannyId,
    decimal LastIncome,
    decimal LastExpenses)
{
    public decimal LastNet =>
        LastIncome - LastExpenses;
}
