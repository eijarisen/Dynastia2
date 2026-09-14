using System.Collections.ObjectModel;

namespace Dynastia.App.ViewModels;

public sealed record FamilyRelationActionViewModel(
    string Id,
    string Label,
    string Description,
    Guid RelativeId,
    bool RequiresPropertySelection,
    bool RequiresMoneySelection);

public sealed record FamilyRelationHouseholdViewModel(
    string HouseholdTitle,
    string RelativesText,
    string RelationshipText,
    string WealthText,
    string LocationText,
    IReadOnlyList<FamilyRelationActionViewModel> Actions);

public sealed class FamilyRelationsWindowViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    private string _statusText = string.Empty;

    public FamilyRelationsWindowViewModel(MainWindowViewModel main)
    {
        _main = main;
        Refresh();
    }

    public ObservableCollection<FamilyRelationHouseholdViewModel> Households { get; } = [];

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatus));
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);

    public void Refresh()
    {
        Households.Clear();
        foreach (var household in _main.GetFamilyRelationsHouseholds())
            Households.Add(household);
        OnPropertyChanged(nameof(Households));
    }

    public IReadOnlyList<PropertySelectionOption> GetGiveHouseOptions(Guid relativeId) =>
        _main.GetFamilyRelationGiveHouseOptions(relativeId);

    public decimal GetMoneyMaximum(FamilyRelationActionViewModel action) =>
        _main.GetFamilyRelationMoneyMaximum(
            action.RelativeId,
            action.Id);

    public void Queue(
        FamilyRelationActionViewModel action,
        string? propertyId = null,
        decimal? moneyAmount = null)
    {
        var result = _main.QueueFamilyRelationAction(
            action.RelativeId,
            action.Id,
            propertyId,
            moneyAmount);
        StatusText = result.Message ?? (result.Success ? "Action queued." : "The action could not be queued.");
        Refresh();
    }
}
