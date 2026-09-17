using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    public decimal GetProjectedAnnualIncome(
        IPerson person) =>
        GetProjectedIncomeBreakdown(person)
            .Sum(item => item.Amount);

    public IReadOnlyList<FinanceBreakdownItem> GetProjectedIncomeBreakdown(
        IPerson person) =>
        CalculateProjectedIncomeBreakdown(person);
}
