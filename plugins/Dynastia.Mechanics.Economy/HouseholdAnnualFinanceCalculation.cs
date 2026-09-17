using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

internal enum AnnualFinanceCalculationMode
{
    Forecast,
    Realized
}

internal sealed record HouseholdAnnualFinanceCalculation(
    decimal Income,
    decimal Expenses,
    IReadOnlyList<FinanceBreakdownItem> IncomeBreakdown,
    IReadOnlyList<FinanceBreakdownItem> ExpenseBreakdown,
    decimal CarefulManagementIncome = 0m)
{
    public decimal Net => Income - Expenses;
}
