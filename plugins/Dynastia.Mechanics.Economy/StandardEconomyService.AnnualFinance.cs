using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    private const double CarefulManagementChance = 0.05;
    private const decimal CarefulManagementReward = 250m;

    internal HouseholdAnnualFinanceCalculation CalculateAnnualFinances(
        IPerson person,
        AnnualFinanceCalculationMode mode)
    {
        var resolved = FindHousehold(person);
        if (resolved is null)
        {
            return new HouseholdAnnualFinanceCalculation(
                0m,
                0m,
                Array.Empty<FinanceBreakdownItem>(),
                Array.Empty<FinanceBreakdownItem>());
        }

        var (owner, household) = resolved.Value;
        var members = GetFinancialHouseholdMembers(household);
        var income = CalculateAnnualIncome(owner, household, members, mode);
        var expenses = CalculateAnnualExpenses(owner, household, members);

        return new HouseholdAnnualFinanceCalculation(
            income.Total,
            expenses.Total,
            income.Lines,
            expenses.Lines,
            income.CarefulManagementIncome);
    }

    internal IReadOnlyList<FinanceBreakdownItem> CalculateProjectedIncomeBreakdown(
        IPerson person)
    {
        var resolved = FindHousehold(person);
        if (resolved is null)
            return Array.Empty<FinanceBreakdownItem>();

        var (owner, household) = resolved.Value;
        return CalculateAnnualIncome(
                owner,
                household,
                GetFinancialHouseholdMembers(household),
                AnnualFinanceCalculationMode.Forecast)
            .Lines;
    }

    private (
        decimal Total,
        IReadOnlyList<FinanceBreakdownItem> Lines,
        decimal CarefulManagementIncome)
        CalculateAnnualIncome(
            IPerson owner,
            HouseholdEconomyComponent household,
            IReadOnlyList<IPerson> members,
            AnnualFinanceCalculationMode mode)
    {
        var lines = new List<FinanceBreakdownItem>();

        foreach (var member in members.Where(candidate => candidate.Age >= 18))
        {
            var amount = RoundCurrency(
                _income.Providers.Sum(provider =>
                    mode == AnnualFinanceCalculationMode.Realized
                        ? provider.GetAnnualIncome(member)
                        : provider.GetExpectedAnnualIncome(member)));

            if (amount != 0m)
            {
                lines.Add(
                    new FinanceBreakdownItem(
                        member.Name,
                        amount,
                        member.Id));
            }
        }

        var rentalIncome = GetHouses(owner)
            .Where(house => house.IsRented)
            .Sum(house => GetRentalIncome(house.Town));

        if (rentalIncome > 0m)
        {
            lines.Add(
                new FinanceBreakdownItem(
                    "houses",
                    rentalIncome));
        }

        foreach (var provider in _householdIncome.Providers)
        {
            var amount = RoundCurrency(
                mode == AnnualFinanceCalculationMode.Realized
                    ? provider.GetAnnualIncome(owner)
                    : provider.GetExpectedAnnualIncome(owner));

            if (amount != 0m)
            {
                lines.Add(
                    new FinanceBreakdownItem(
                        provider.Label,
                        amount));
            }
        }

        decimal carefulManagementIncome = 0m;
        if (mode == AnnualFinanceCalculationMode.Realized
            && HasExceptionalIntellect(owner)
            && _random.NextDouble() < CarefulManagementChance)
        {
            carefulManagementIncome = CarefulManagementReward;
            lines.Add(
                new FinanceBreakdownItem(
                    "careful management",
                    carefulManagementIncome));
        }

        return (
            RoundCurrency(lines.Sum(line => line.Amount)),
            lines,
            carefulManagementIncome);
    }

    private (
        decimal Total,
        IReadOnlyList<FinanceBreakdownItem> Lines)
        CalculateAnnualExpenses(
            IPerson owner,
            HouseholdEconomyComponent household,
            IReadOnlyList<IPerson> members)
    {
        var lines = new List<FinanceBreakdownItem>();
        var homeTown = _locations.GetLocation(owner).HomeTown;

        var livingCosts = EconomyAnnualRules.CalculateLivingCosts(
            members.Count,
            GetLivingCostPerPerson(homeTown),
            HasExceptionalIntellect(owner));

        if (livingCosts > 0m)
        {
            lines.Add(
                new FinanceBreakdownItem(
                    "living costs",
                    livingCosts));
        }

        var ownsLocalResidence = household.Houses.Any(house =>
            !string.IsNullOrWhiteSpace(house.TownId)
            && house.TownId.Equals(
                homeTown.Id,
                StringComparison.OrdinalIgnoreCase));

        if (!ownsLocalResidence)
        {
            lines.Add(
                new FinanceBreakdownItem(
                    "rented home",
                    GetResidenceRent(homeTown)));
        }

        if (EconomyAnnualRules.ShouldChargeNanny(_gameState, household))
        {
            lines.Add(
                new FinanceBreakdownItem(
                    "nanny",
                    EconomyAnnualRules.NannyExpense));
        }

        return (
            RoundCurrency(lines.Sum(line => line.Amount)),
            lines);
    }

    internal IReadOnlyList<IPerson> GetFinancialHouseholdMembers(
        HouseholdEconomyComponent household) =>
        household.MemberIds
            .Select(id =>
                _gameState.People.FirstOrDefault(candidate => candidate.Id == id))
            .Where(candidate =>
                candidate is not null
                && candidate.Tags.Has("state.alive")
                && !candidate.Tags.Has("role.nanny"))
            .Cast<IPerson>()
            .DistinctBy(candidate => candidate.Id)
            .ToList();

    internal bool HasExceptionalIntellect(IPerson person) =>
        _stats.GetStats(person)
            .Any(stat =>
                stat.Id.Equals("intellect", StringComparison.OrdinalIgnoreCase)
                && stat.Value == 5);
}
