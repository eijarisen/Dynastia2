using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    public HouseholdAnnualForecast? GetAnnualForecast(IPerson person)
    {
        var resolved = FindHousehold(person);
        if (resolved is null)
            return null;

        var (owner, household) = resolved.Value;
        SynchronizeHouses(owner, household);

        var incomeLines = GetProjectedIncomeBreakdown(owner).ToList();
        var expenseLines = new List<FinanceBreakdownItem>();

        var members = household.MemberIds
            .Select(id => _gameState.People.FirstOrDefault(candidate => candidate.Id == id))
            .Where(candidate =>
                candidate is not null
                && candidate.Tags.Has("state.alive")
                && !candidate.Tags.Has("role.nanny"))
            .Cast<IPerson>()
            .DistinctBy(candidate => candidate.Id)
            .ToList();

        var homeTown = _locations.GetLocation(owner).HomeTown;
        var exceptionalIntellect = _stats.GetStats(owner).Any(stat =>
            stat.Id.Equals("intellect", StringComparison.OrdinalIgnoreCase)
            && stat.Value == 5);

        var livingCosts = EconomyAnnualRules.CalculateLivingCosts(
            members.Count,
            GetLivingCostPerPerson(homeTown),
            exceptionalIntellect);

        if (livingCosts > 0)
            expenseLines.Add(new FinanceBreakdownItem("living costs", livingCosts));

        var ownsLocalResidence = household.Houses.Any(house =>
            house.Town is not null
            && house.Town.Id.Equals(homeTown.Id, StringComparison.OrdinalIgnoreCase));

        if (!ownsLocalResidence)
        {
            expenseLines.Add(
                new FinanceBreakdownItem(
                    "rented home",
                    GetResidenceRent(homeTown)));
        }

        if (EconomyAnnualRules.ShouldChargeNanny(_gameState, household))
        {
            expenseLines.Add(
                new FinanceBreakdownItem(
                    "nanny",
                    EconomyAnnualRules.NannyExpense));
        }

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
