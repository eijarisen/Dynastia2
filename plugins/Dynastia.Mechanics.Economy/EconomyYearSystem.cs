using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class EconomyYearSystem : IYearSystem
{
    private readonly StandardEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly IGameEventBus _events;

    public EconomyYearSystem(
        StandardEconomyService economy,
        IFamilyService family,
        IGameEventBus events)
    {
        _economy = economy;
        _family = family;
        _events = events;
    }

    public string Id =>
        "economy.household_finances";

    public YearPhase Phase =>
        YearPhase.Finances;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var head in gameState.People
            .Where(person =>
                person.Tags.Has("state.alive")
                && !SimulationState.IsExternallyResident(person)
                && _economy.HasHousehold(person))
            .ToList())
        {
            ProcessHousehold(gameState, head);
        }
    }

    private void ProcessHousehold(
        IGameState gameState,
        IPerson head)
    {
        var household = _economy.GetRequiredHousehold(head);
        if (household.EstateReady)
            return;

        _economy.SynchronizeForFinance(head, household);

        var calculation = _economy.CalculateAnnualFinances(
            head,
            AnnualFinanceCalculationMode.Realized);

        household.LastIncomeBreakdown.Clear();
        foreach (var line in calculation.IncomeBreakdown)
        {
            household.LastIncomeBreakdown.Add(
                new LedgerLineState
                {
                    Label = line.Label,
                    Amount = line.Amount,
                    PersonId = line.PersonId
                });
        }

        household.LastExpenseBreakdown.Clear();
        foreach (var line in calculation.ExpenseBreakdown)
        {
            household.LastExpenseBreakdown.Add(
                new LedgerLineState
                {
                    Label = line.Label,
                    Amount = line.Amount,
                    PersonId = line.PersonId
                });
        }

        household.LastIncome = calculation.Income;
        household.LastExpenses = calculation.Expenses;

        var funding = EconomyBalanceRules.CalculateBasicNeedsFunding(
            household.Wealth,
            calculation.Income,
            calculation.Expenses);
        household.FundingYear = gameState.Year;
        household.BasicNeedsRequired = funding.Required;
        household.BasicNeedsFunded = funding.Funded;
        household.BasicNeedsShortfall = funding.Shortfall;

        household.Wealth = EconomyBalanceRules.ApplyOrdinaryAnnualFinance(
            household.Wealth,
            calculation.Income,
            calculation.Expenses);

        _economy.RecordBudgetHistory(
            household,
            gameState.Year);

        if (calculation.CarefulManagementIncome > 0m)
        {
            _events.Publish(
                new GameEvent
                {
                    Type = "economy.careful_management",
                    Year = gameState.Year,
                    SubjectId = head.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["amount"] = calculation.CarefulManagementIncome.ToString(),
                        ["text"] =
                            $"{_family.GetDisplayName(head)}'s careful management " +
                            $"brought an additional {calculation.CarefulManagementIncome:N0} zł " +
                            "into the household."
                    }
                });
        }
    }
}
