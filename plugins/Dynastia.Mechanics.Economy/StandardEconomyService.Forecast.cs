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


    public bool HasSufficientPassiveIncomeForBasicNeeds(IPerson person)
    {
        var resolved = FindHousehold(person);
        if (resolved is null)
            return false;

        var (owner, household) = resolved.Value;
        var members = GetFinancialHouseholdMembers(household);

        var passiveIncome = members
            .Where(member => member.Age >= 18)
            .Sum(member => _income.Providers.Sum(provider =>
                provider.GetExpectedPassiveAnnualIncome(member)));

        passiveIncome += GetHouses(owner)
            .Where(house => house.IsRented)
            .Sum(GetRentalIncome);

        passiveIncome += _householdIncome.Providers.Sum(provider =>
            provider.GetExpectedPassiveAnnualIncome(owner));

        passiveIncome += _financeProjections.Providers.Sum(provider =>
            provider.GetProjectedPassiveIncome(owner));

        var core = CalculateAnnualFinances(
            owner,
            AnnualFinanceCalculationMode.Forecast);
        var projectedExpenses = core.Expenses
            + _financeProjections.Providers.Sum(provider =>
                provider.GetProjectedExpenses(owner).Sum(line => line.Amount));

        var funding = EconomyBalanceRules.CalculateBasicNeedsFunding(
            household.Wealth,
            RoundCurrency(passiveIncome),
            RoundCurrency(projectedExpenses));

        return funding.Shortfall <= 0m;
    }

}
