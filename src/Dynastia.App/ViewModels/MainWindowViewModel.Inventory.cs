using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string ManagePropertiesUiActionId =
        "ui.manage_properties";

    private static GameActionDefinition CreateManagePropertiesPresentationAction() =>
        new()
        {
            Id = ManagePropertiesUiActionId,
            Label = "Manage Properties",
            Description =
                "Open Family Inventory to buy or sell houses and farmland.",
            Mode = ActionExecutionMode.Immediate,
            IsAvailable = _ => true,
            Execute = _ => new GameActionResult(false)
        };

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

        var farming =
            _farmingService?.GetSnapshot(actor);

        var forecast =
            _economyService.GetAnnualForecast(actor);

        var incomeLines =
            (forecast?.IncomeBreakdown
                ?? finance.LastIncomeBreakdown)
            .Select(FormatInventoryIncomeLine)
            .ToList();

        var incomeTotal =
            forecast?.ProjectedIncome
            ?? finance.LastIncome;

        var debts =
            _loanService?.GetDebts(actor)
            ?? Array.Empty<LoanContractInfo>();

        var loansGiven =
            _loanService?.GetLoansGiven(actor)
            ?? Array.Empty<LoanContractInfo>();

        var expenseLines =
            (forecast?.ExpenseBreakdown
                ?? finance.LastExpenseBreakdown)
            .ToList();

        var projectedExpenses =
            forecast?.ProjectedExpenses
            ?? finance.LastExpenses;

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
            projectedExpenses,
            incomeLines,
            expenseLines,
            debts,
            loansGiven,
            _economyService.GetHouses(actor),
            farming,
            children,
            CanUseFamilyInventoryAction("loan.take"),
            CanUseFamilyInventoryAction("loan.give"),
            CanUseFamilyInventoryAction("household.buy_house"),
            CanUseFamilyInventoryAction("household.sell_house"),
            CanUseFamilyInventoryAction("farming.buy_farmland"),
            CanUseFamilyInventoryAction("farming.sell_farmland"),
            _farmingService?.PurchasePrice ?? 10000m,
            _farmingService?.SalePrice ?? 8000m);
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

    internal bool SetFarmlandInheritanceHeir(
        Guid farmlandId,
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
            _economyService.SetFarmlandInheritanceHeir(
                actor,
                farmlandId,
                heirId);

        if (!changed)
            return false;

        RefreshEconomy();
        OnPropertyChanged(
            nameof(HouseholdHousesDetailsText));
        return true;
    }

    internal GameActionResult QueueFamilyInventoryAction(
        string actionId)
    {
        var actor = _succession.ActiveController;
        if (actor is null)
            return new GameActionResult(false, "No active household is available.");

        var result = _actionRegistry.Execute(
            actionId,
            actor,
            actor);

        if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
            PersistenceStatusText = result.Message;

        RefreshActions();
        RefreshEconomy();
        OnPropertyChanged(nameof(HasQueuedAction));
        OnPropertyChanged(nameof(QueuedActionText));
        return result;
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

    private FinanceBreakdownItem FormatInventoryIncomeLine(
        FinanceBreakdownItem line)
    {
        if (line.PersonId is not Guid personId)
            return line;

        var person =
            _gameState.People.FirstOrDefault(
                candidate => candidate.Id == personId);

        if (person is null)
            return line;

        var name =
            _familyService?.GetDisplayName(person)
            ?? $"{person.Name} {person.Surname}";

        var emoji =
            _careerPresentationService?
                .GetOccupationEmoji(person)
            ?? CareerPresentationDefaults.DefaultCareerEmoji;

        if (_careerService is null)
        {
            return line with
            {
                Label = $"{emoji} {name}"
            };
        }

        var career =
            _careerService.GetCareer(person);

        var occupation =
            career.IsEmployed && career.JobLevel > 0
                ? $"{career.JobTitle} (L{career.JobLevel})"
                : career.JobTitle;

        var amount = line.Amount;
        if (_craftService is not null && career.IsSelfEmployed)
        {
            var craft = _craftService.GetSnapshot(person);
            if (craft.LastIncomeYear == _gameState.Year)
                amount = craft.LastAnnualIncome;
        }

        return line with
        {
            Label = $"{emoji} {name} — {occupation}",
            Amount = amount
        };
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
    FarmingHouseholdSnapshot? Farming,
    IReadOnlyList<FamilyInventoryChildData> Children,
    bool CanTakeLoan,
    bool CanGiveLoan,
    bool CanBuyHouse,
    bool CanSellHouse,
    bool CanBuyFarmland,
    bool CanSellFarmland,
    decimal FarmlandPurchasePrice,
    decimal FarmlandSalePrice);

internal sealed record FamilyInventoryChildData(
    Guid Id,
    string Name);
