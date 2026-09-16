using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    public decimal GetProjectedAnnualIncome(
        IPerson person) =>
        GetProjectedIncomeBreakdown(person)
            .Sum(item => item.Amount);

    public IReadOnlyList<FinanceBreakdownItem> GetProjectedIncomeBreakdown(
        IPerson person)
    {
        var resolved =
            FindHousehold(person);

        if (resolved is null)
            return Array.Empty<FinanceBreakdownItem>();

        var (owner, household) =
            resolved.Value;

        SynchronizeHouses(
            owner,
            household);

        var lines =
            new List<FinanceBreakdownItem>();

        foreach (var member in
            household.MemberIds
                .Select(id =>
                    _gameState.People.FirstOrDefault(
                        candidate => candidate.Id == id))
                .Where(candidate =>
                    candidate is not null
                    && candidate.Tags.Has("state.alive")
                    && !candidate.Tags.Has("role.nanny")
                    && candidate.Age >= 18)
                .Cast<IPerson>()
                .DistinctBy(candidate => candidate.Id))
        {
            var amount =
                RoundCurrency(
                    _income.GetAnnualIncome(member));

            if (amount == 0)
                continue;

            lines.Add(
                new FinanceBreakdownItem(
                    member.Name,
                    amount,
                    member.Id));
        }

        var rentalIncome =
            GetHouses(owner)
                .Where(house => house.IsRented)
                .Sum(house =>
                    GetRentalIncome(house.Town));

        if (rentalIncome > 0)
        {
            lines.Add(
                new FinanceBreakdownItem(
                    "houses",
                    rentalIncome));
        }

        return lines;
    }
}
