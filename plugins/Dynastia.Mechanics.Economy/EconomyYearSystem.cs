using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class EconomyYearSystem :
    IYearSystem
{
    private const decimal LivingExpense =
        250m;

    private const decimal RentalIncomePerHouse =
        250m;

    private const decimal RentExpense =
        250m;

    private const decimal NannyExpense =
        250m;

    private readonly StandardEconomyService _economy;
    private readonly IIncomeProviderRegistry _income;

    public EconomyYearSystem(
        StandardEconomyService economy,
        IFamilyService family,
        IIncomeProviderRegistry income)
    {
        _economy =
            economy;

        _income =
            income;
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

        var rentalHouses =
            Math.Max(
                0,
                household.HousesOwned
                - 1);

        var rentalIncome =
            rentalHouses
            * RentalIncomePerHouse;

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

        household.LastExpenseBreakdown.Clear();

        var livingCosts =
            members.Count
            * LivingExpense;

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

        if (household.HousesOwned == 0)
        {
            expenses +=
                RentExpense;

            household.LastExpenseBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        "rented home",

                    Amount =
                        RentExpense
                });
        }

        if (ShouldChargeNanny(
            gameState,
            household))
        {
            expenses +=
                NannyExpense;

            household.LastExpenseBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        "nanny",

                    Amount =
                        NannyExpense
                });
        }

        household.LastIncome =
            income;

        household.LastExpenses =
            expenses;

        household.Wealth =
            Math.Max(
                0,
                household.Wealth
                + income
                - expenses);
    }

    private static bool ShouldChargeNanny(
        IGameState gameState,
        HouseholdEconomyComponent household)
    {
        if (household.NannyId
            is not Guid nannyId)
        {
            return false;
        }

        var nanny =
            gameState.People
                .FirstOrDefault(
                    person =>
                        person.Id
                        == nannyId);

        return nanny is null
            || !nanny.Tags.Has(
                "role.family_nanny");
    }

    private decimal AddPersonIncome(
        HouseholdEconomyComponent household,
        IPerson person)
    {
        var amount =
            _income.GetAnnualIncome(
                person);

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
