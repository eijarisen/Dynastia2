using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    internal bool CanOpenFamilyInventory =>
        IsLineageFamilyView
        && _succession.ActiveController is not null
        && _economyService is not null;

    internal FamilyInventoryData? GetFamilyInventoryData()
    {
        var actor = _succession.ActiveController;

        if (actor is null
            || _economyService is null)
        {
            return null;
        }

        var finance =
            _economyService.GetHousehold(actor);

        if (finance is null)
            return null;

        var incomeLines =
            _economyService
                .GetProjectedIncomeBreakdown(actor)
                .ToList();

        var loanIncome =
            GetProjectedLoanIncome(actor);

        if (loanIncome > 0)
        {
            incomeLines.Add(
                new FinanceBreakdownItem(
                    "loan repayments",
                    loanIncome));
        }

        var incomeTotal =
            _economyService.GetProjectedAnnualIncome(actor)
            + loanIncome;

        var debts =
            _loanService?.GetDebts(actor)
            ?? Array.Empty<LoanContractInfo>();

        var loansGiven =
            _loanService?.GetLoansGiven(actor)
            ?? Array.Empty<LoanContractInfo>();

        var expenseLines =
            finance.LastExpenseBreakdown
                .ToList();

        var debtPayments =
            debts.Sum(debt => debt.AnnualPayment);

        if (debtPayments > 0)
        {
            expenseLines.Add(
                new FinanceBreakdownItem(
                    "loan repayments",
                    debtPayments));
        }

        var children =
            (_familyService?.GetChildren(actor)
                ?? Array.Empty<IPerson>())
            .Where(child =>
                child.Tags.Has("state.alive"))
            .OrderBy(child =>
                child.BirthDate?.Year
                ?? int.MaxValue)
            .ThenBy(child =>
                child.BirthDate?.Month
                ?? 1)
            .ThenBy(child =>
                child.BirthDate?.Day
                ?? 1)
            .ThenBy(child => child.Id)
            .Select(child =>
                new FamilyInventoryChildData(
                    child.Id,
                    _familyService?.GetDisplayName(child)
                    ?? $"{child.Name} {child.Surname}"))
            .ToList();

        var householdName =
            _familyService?.GetDisplayName(actor)
            ?? $"{actor.Name} {actor.Surname}";

        return new FamilyInventoryData(
            householdName,
            finance.Wealth,
            incomeTotal,
            finance.LastExpenses + debtPayments,
            incomeLines,
            expenseLines,
            debts,
            loansGiven,
            _economyService.GetHouses(actor),
            children,
            CanUseFamilyInventoryAction("loan.take"),
            CanUseFamilyInventoryAction("loan.give"),
            CanUseFamilyInventoryAction("household.buy_house"),
            CanUseFamilyInventoryAction("household.sell_house"));
    }

    internal bool SetHouseInheritanceHeir(
        Guid propertyId,
        Guid? heirId)
    {
        var actor =
            _succession.ActiveController;

        if (actor is null
            || _economyService is null)
        {
            return false;
        }

        var changed =
            _economyService.SetHouseInheritanceHeir(
                actor,
                propertyId,
                heirId);

        if (!changed)
            return false;

        RefreshEconomy();

        OnPropertyChanged(
            nameof(HouseholdHousesDetailsText));

        return true;
    }

    private bool CanUseFamilyInventoryAction(
        string actionId)
    {
        var actor =
            _succession.ActiveController;

        if (actor is null
            || _succession.IsGameOver)
        {
            return false;
        }

        return _actionRegistry
            .GetAvailableActions(actor, actor)
            .Any(action =>
                action.Id.Equals(
                    actionId,
                    StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed record FamilyInventoryData(
    string HouseholdName,
    decimal Budget,
    decimal IncomeTotal,
    decimal ExpenseTotal,
    IReadOnlyList<FinanceBreakdownItem> IncomeLines,
    IReadOnlyList<FinanceBreakdownItem> ExpenseLines,
    IReadOnlyList<LoanContractInfo> Debts,
    IReadOnlyList<LoanContractInfo> LoansGiven,
    IReadOnlyList<HousePropertyInfo> Houses,
    IReadOnlyList<FamilyInventoryChildData> Children,
    bool CanTakeLoan,
    bool CanGiveLoan,
    bool CanBuyHouse,
    bool CanSellHouse);

internal sealed record FamilyInventoryChildData(
    Guid Id,
    string Name);
