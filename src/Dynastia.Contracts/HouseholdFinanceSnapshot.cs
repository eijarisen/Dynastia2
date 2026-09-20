namespace Dynastia.Contracts;

public sealed record HouseholdFinanceSnapshot(
    decimal Wealth,
    int HousesOwned,
    int RentedHouses,
    decimal PendingInheritance,
    int PendingHouses,
    Guid? NannyId,
    decimal LastIncome,
    decimal LastExpenses,
    IReadOnlyList<FinanceBreakdownItem> LastIncomeBreakdown,
    IReadOnlyList<FinanceBreakdownItem> LastExpenseBreakdown,
    IReadOnlyList<HousePropertyInfo> Houses)
{
    public decimal LastNet =>
        LastIncome - LastExpenses;

    public IReadOnlyList<HouseholdBudgetHistoryPoint> History { get; init; } =
        Array.Empty<HouseholdBudgetHistoryPoint>();

    public HouseholdLifestyleStance Lifestyle { get; init; } =
        HouseholdLifestyleStance.Balanced;
}
