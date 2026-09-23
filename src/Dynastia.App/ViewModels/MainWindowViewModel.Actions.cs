using System.Collections.ObjectModel;
using Dynastia.App.ViewModels.Actions;
using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public event EventHandler<ActionSelectionRequestedEventArgs>? ActionSelectionRequested;

    public ObservableCollection<AvailableActionViewModel> AvailableActions => _actionPanel.AvailableActions;
    public ObservableCollection<AvailableActionViewModel> PassActions => _actionPanel.PassActions;
    public ObservableCollection<ActionFilterViewModel> ActionFilters => _actionPanel.ActionFilters;
    public bool HasQueuedAction => _actionPanel.HasQueuedAction;
    public string QueuedActionText => _actionPanel.QueuedActionText;
    public string ActionsEmptyText => _actionPanel.GetEmptyText(
        IsBloodlineFamilyView, IsBloodlineFamilyView && GetDisplayedHouseholdHead() is not null);

    public bool TryExecuteAvailableActionShortcut(params string[] actionIds) =>
        _actionPanel.TryExecuteAvailableActionShortcut(IsGameStarted, IsMainMenuPromptVisible, actionIds);

    private void RefreshActions() => _actionPanel.Refresh(IsBloodlineFamilyView);
    private void ResetActionCategoryFilters() => _actionPanel.ResetActionCategoryFilters();

    public IReadOnlyList<PropertySelectionOption> GetPropertySelectionOptions(string actionId) =>
        _actionSelectionOptions.GetPropertySelectionOptions(actionId);

    public void QueueActionWithSelection(string actionId, string selectedId) =>
        _actionPanel.QueueActionWithSelection(actionId, selectedId);

    public decimal GetMaximumLoanPrincipal(string actionId) =>
        _actionSelectionOptions.GetMaximumLoanPrincipal(actionId);

    public IReadOnlyList<LoanOfferInfo> GetLoanOffers(bool isGivingLoan, decimal maximumPrincipal) =>
        _actionSelectionOptions.GetLoanOffers(isGivingLoan, maximumPrincipal);

    public LoanTermsInfo? GetLoanTerms(decimal principal, int durationYears) =>
        _actionSelectionOptions.GetLoanTerms(principal, durationYears);

    public void QueueLoanAction(string actionId, LoanSelectionResult selection) =>
        _actionPanel.QueueLoanAction(actionId, selection);

    private void OnActionUiExecuted(object? sender, ActionUiExecutionResult result)
    {
        if (!result.Attempted)
            return;

        if (result.FromSelection)
        {
            if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
                PersistenceStatusText = result.Message;
        }
        else if (result.Success
            && GetMaleHeirsWithoutAnnualAction().Count == 0
            && PersistenceStatusText.StartsWith("Choose ", StringComparison.OrdinalIgnoreCase))
        {
            PersistenceStatusText = string.Empty;
        }

        if (!result.StateMayHaveChanged)
            return;

        RefreshPeople();
        RefreshAlbum();
        RefreshHealth();
        RefreshEconomy();
        RefreshEducation();
        RefreshCareer();
        RefreshJustice();
        RefreshNarrative();
        RefreshActions();
    }

    private IPerson? FindSelectedPerson()
    {
        if (SelectedPerson is null)
            return null;

        return _gameState.People
            .FirstOrDefault(
                x =>
                    x.Id
                    == SelectedPerson.Id);
    }

}
