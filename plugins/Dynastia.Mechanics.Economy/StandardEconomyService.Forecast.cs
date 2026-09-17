using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    public HouseholdAnnualForecast? GetAnnualForecast(IPerson person)
    {
        var resolved = FindHousehold(person);
        if (resolved is null)
            return null;

        var owner = resolved.Value.Owner;

        var core = CalculateAnnualFinances(
            person,
            AnnualFinanceCalculationMode.Forecast);

        var incomeLines = core.IncomeBreakdown.ToList();
        var expenseLines = core.ExpenseBreakdown.ToList();

        foreach (var provider in _financeProjections.Providers)
        {
            incomeLines.AddRange(provider.GetProjectedIncome(owner));
            expenseLines.AddRange(provider.GetProjectedExpenses(owner));
        }

        var projectedIncome = RoundCurrency(incomeLines.Sum(line => line.Amount));
        var projectedExpenses = RoundCurrency(expenseLines.Sum(line => line.Amount));

        return new HouseholdAnnualForecast(
            projectedIncome,
            projectedExpenses,
            incomeLines,
            expenseLines);
    }
}
