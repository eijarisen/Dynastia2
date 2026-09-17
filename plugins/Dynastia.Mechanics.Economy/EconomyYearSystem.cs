using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class EconomyYearSystem :
    IYearSystem
{
    private const double CarefulManagementChance =
        0.05;

    private const decimal CarefulManagementReward =
        250m;

    private readonly StandardEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly IIncomeProviderRegistry _income;
    private readonly IHouseholdIncomeProviderRegistry _householdIncome;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly ILocationService _locations;

    public EconomyYearSystem(
        StandardEconomyService economy,
        IFamilyService family,
        IStatsService stats,
        IIncomeProviderRegistry income,
        IHouseholdIncomeProviderRegistry householdIncome,
        IGameRandom random,
        IGameEventBus events,
        ILocationService locations)
    {
        _economy =
            economy;

        _family =
            family;

        _stats =
            stats;

        _income =
            income;

        _householdIncome =
            householdIncome;

        _random =
            random;

        _events =
            events;

        _locations =
            locations;
    }

    public string Id =>
        "economy.household_finances";

    public YearPhase Phase =>
        YearPhase.Finances;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        foreach (var head in
            gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive")
                        && _economy.HasHousehold(
                            person))
                .ToList())
        {
            ProcessHousehold(
                gameState,
                head);
        }
    }

    private void ProcessHousehold(
        IGameState gameState,
        IPerson head)
    {
        var household =
            _economy.GetRequiredHousehold(
                head);

        if (household.EstateReady)
            return;

        _economy.SynchronizeForFinance(
            head,
            household);

        var members =
            household.MemberIds
                .Select(
                    id =>
                        gameState.People
                            .FirstOrDefault(
                                person =>
                                    person.Id
                                    == id))
                .Where(
                    person =>
                        person is not null
                        && person.Tags.Has(
                            "state.alive")
                        && !person.Tags.Has(
                            "role.nanny"))
                .Cast<IPerson>()
                .DistinctBy(
                    person =>
                        person.Id)
                .ToList();

        household.LastIncomeBreakdown.Clear();

        decimal income =
            0;

        foreach (var member in
            members.Where(
                person =>
                    person.Age >= 18))
        {
            income +=
                AddPersonIncome(
                    household,
                    member);
        }

        var rentalIncome =
            _economy.GetHouses(head)
                .Where(house => house.IsRented)
                .Sum(house => _economy.GetRentalIncome(house.Town));

        if (rentalIncome > 0)
        {
            household.LastIncomeBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        "houses",

                    Amount =
                        rentalIncome
                });
        }

        income +=
            rentalIncome;

        foreach (var provider in _householdIncome.Providers)
        {
            var householdAmount =
                Math.Round(
                    provider.GetAnnualIncome(head),
                    0,
                    MidpointRounding.AwayFromZero);

            household.LastIncomeBreakdown.Add(
                new LedgerLineState
                {
                    Label = provider.Label,
                    Amount = householdAmount
                });

            income += householdAmount;
        }

        if (HasExceptionalIntellect(head)
            && _random.NextDouble()
                < CarefulManagementChance)
        {
            income +=
                CarefulManagementReward;

            household.LastIncomeBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        "careful management",

                    Amount =
                        CarefulManagementReward
                });

            _events.Publish(
                new GameEvent
                {
                    Type =
                        "economy.careful_management",

                    Year =
                        gameState.Year,

                    SubjectId =
                        head.Id,

                    Data =
                        new Dictionary<string, string>
                        {
                            ["amount"] =
                                CarefulManagementReward.ToString(),

                            ["text"] =
                                $"{_family.GetDisplayName(head)}'s careful " +
                                $"management brought an additional " +
                                $"{CarefulManagementReward:N0} zł into " +
                                "the household."
                        }
                });
        }

        household.LastExpenseBreakdown.Clear();

        var homeTown = _locations.GetLocation(head).HomeTown;

        var livingCosts =
            EconomyAnnualRules.CalculateLivingCosts(
                members.Count,
                _economy.GetLivingCostPerPerson(homeTown),
                HasExceptionalIntellect(head));

        if (livingCosts > 0)
        {
            household.LastExpenseBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        "living costs",

                    Amount =
                        livingCosts
                });
        }

        var expenses =
            livingCosts;

        var ownsLocalResidence = household.Houses.Any(house =>
            house.Town is not null
            && house.Town.Id.Equals(homeTown.Id, StringComparison.OrdinalIgnoreCase));

        if (!ownsLocalResidence)
        {
            var localRent = _economy.GetResidenceRent(homeTown);
            expenses += localRent;

            household.LastExpenseBreakdown.Add(
                new LedgerLineState
                {
                    Label = "rented home",
                    Amount = localRent
                });
        }

        if (EconomyAnnualRules.ShouldChargeNanny(
            gameState,
            household))
        {
            expenses +=
                EconomyAnnualRules.NannyExpense;

            household.LastExpenseBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        "nanny",

                    Amount =
                        EconomyAnnualRules.NannyExpense
                });
        }

        household.LastIncome =
            income;

        household.LastExpenses =
            expenses;

        // Ordinary expenses still cannot create debt, but an existing
        // negative balance created by loan repayments must survive. Income
        // first fills that balance; living costs are charged only against a
        // non-negative balance and therefore never deepen loan debt.
        household.Wealth =
            EconomyBalanceRules.ApplyOrdinaryAnnualFinance(
                household.Wealth,
                income,
                expenses);
    }


    private bool HasExceptionalIntellect(
        IPerson head)
    {
        return _stats.GetStats(head)
            .Any(stat =>
                stat.Id.Equals(
                    "intellect",
                    StringComparison.OrdinalIgnoreCase)
                && stat.Value == 5);
    }

    private decimal AddPersonIncome(
        HouseholdEconomyComponent household,
        IPerson person)
    {
        var amount =
            Math.Round(
                _income.GetAnnualIncome(
                    person),
                0,
                MidpointRounding.AwayFromZero);

        if (amount != 0)
        {
            household.LastIncomeBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        person.Name,

                    Amount =
                        amount
                });
        }

        return amount;
    }
}
