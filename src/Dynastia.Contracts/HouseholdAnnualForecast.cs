namespace Dynastia.Contracts;

public sealed record HouseholdAnnualForecast(
    decimal ProjectedIncome,
    decimal ProjectedExpenses,
    IReadOnlyList<FinanceBreakdownItem> IncomeBreakdown,
    IReadOnlyList<FinanceBreakdownItem> ExpenseBreakdown)
{
    public decimal ProjectedNet =>
        ProjectedIncome - ProjectedExpenses;
}
