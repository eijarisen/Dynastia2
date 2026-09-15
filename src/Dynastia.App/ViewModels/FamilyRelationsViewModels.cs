using System.Collections.ObjectModel;
using Avalonia.Media;

namespace Dynastia.App.ViewModels;

public sealed record FamilyRelationActionViewModel(
    string Id,
    string Label,
    string Description,
    Guid RelativeId,
    bool RequiresPropertySelection,
    bool RequiresMoneySelection);

public sealed record FamilyRelationHouseholdViewModel(
    Guid HouseholdHeadId,
    Guid RelativeId,
    bool IsPlayableHousehold,
    string Kinship,
    string RelativeName,
    string FamiliarityState,
    string SympathyState,
    string OtherMembersText,
    string TownText,
    string WealthText,
    string HousesText,
    IReadOnlyList<FamilyRelationActionViewModel> Actions)
{
    public string RelationState => $"{FamiliarityState} · {SympathyState}";

    public IBrush StateBrush => SympathyState switch
    {
        "Hostile" => new SolidColorBrush(Color.Parse("#9B2F2F")),
        "Cold" => new SolidColorBrush(Color.Parse("#B56432")),
        "Warm" => new SolidColorBrush(Color.Parse("#4D7844")),
        "Affectionate" => new SolidColorBrush(Color.Parse("#2F6938")),
        _ => new SolidColorBrush(Color.Parse("#806633"))
    };
}

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
        _main.GetFamilyRelationMoneyMaximum(action.RelativeId, action.Id);

    public void OpenPlayableHousehold(
        FamilyRelationHouseholdViewModel household)
    {
        if (!household.IsPlayableHousehold)
            return;

        if (_main.SelectPlayableFamilyRelationHousehold(
                household.HouseholdHeadId,
                household.RelativeId))
        {
            StatusText = string.Empty;
            Refresh();
        }
    }

    public bool Queue(
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
        if (!result.Success)
            Refresh();
        return result.Success;
    }
}
