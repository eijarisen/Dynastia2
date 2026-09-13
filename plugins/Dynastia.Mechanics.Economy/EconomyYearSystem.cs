using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class EconomyYearSystem :
    IYearSystem
{
    private const decimal RentalIncomePerHouse =
        250m;

    private const decimal NannyExpense =
        250m;

    private const decimal EfficientHouseholdMultiplier =
        0.95m;

    private const double CarefulManagementChance =
        0.05;

    private const decimal CarefulManagementReward =
        250m;

    private readonly StandardEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly IIncomeProviderRegistry _income;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly ITownEconomyService _townEconomy;

    public EconomyYearSystem(
        StandardEconomyService economy,
        IFamilyService family,
        IStatsService stats,
        IIncomeProviderRegistry income,
        IGameRandom random,
        IGameEventBus events,
        ITownEconomyService townEconomy)
    {
        _economy =
            economy;

        _family =
            family;

        _stats =
            stats;

        _income =
            income;

        _random =
            random;

        _events =
            events;

        _townEconomy =
            townEconomy;
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
                household.RentedHouses);

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

        var residenceTown =
            _economy.GetResidenceTown(
                head)
            ?? throw new InvalidOperationException(
                "Household residence town is unavailable.");

        var localEconomy =
            _townEconomy.GetProfile(
                residenceTown);

        var livingCosts =
            members.Count
            * localEconomy.LivingCostUnit;

        if (HasExceptionalIntellect(head))
        {
            livingCosts *=
                EfficientHouseholdMultiplier;
        }

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

        if (!_economy.HasHouseInTown(
                head,
                residenceTown.Id))
        {
            expenses +=
                localEconomy.RentCost;

            household.LastExpenseBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        $"rented home ({residenceTown.Town})",

                    Amount =
                        localEconomy.RentCost
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
