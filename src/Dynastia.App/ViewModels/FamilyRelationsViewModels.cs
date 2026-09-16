using System.Collections.ObjectModel;
using Avalonia.Media;

namespace Dynastia.App.ViewModels;

public sealed class FamilyRelationActionViewModel
{
    public FamilyRelationActionViewModel(
        string id,
        string label,
        string description,
        Guid relativeId,
        bool requiresPropertySelection,
        bool requiresMoneySelection,
        IReadOnlyList<PropertySelectionOption>? inlinePropertyOptions = null)
    {
        Id = id;
        Label = label;
        Description = description;
        RelativeId = relativeId;
        RequiresPropertySelection = requiresPropertySelection;
        RequiresMoneySelection = requiresMoneySelection;
        InlinePropertyOptions = inlinePropertyOptions ?? [];
        SelectedInlineProperty = InlinePropertyOptions.FirstOrDefault();
    }

    public string Id { get; }
    public string Label { get; }
    public string Description { get; }
    public Guid RelativeId { get; }
    public bool RequiresPropertySelection { get; }
    public bool RequiresMoneySelection { get; }
    public IReadOnlyList<PropertySelectionOption> InlinePropertyOptions { get; }
    public PropertySelectionOption? SelectedInlineProperty { get; set; }
    public bool HasInlinePropertySelection => InlinePropertyOptions.Count > 0;
}

public sealed record FamilyRelationMemberViewModel(
    string PortraitEmoji,
    string Text);

public sealed record FamilyRelationHouseholdViewModel(
    Guid HouseholdHeadId,
    Guid RelativeId,
    bool IsPlayableHousehold,
    string Kinship,
    string RelativeName,
    string FamiliarityState,
    string SympathyState,
    IReadOnlyList<FamilyRelationMemberViewModel> Members,
    string TownText,
    string WealthText,
    string HousesText,
    string FarmlandText,
    IReadOnlyList<FamilyRelationActionViewModel> Actions)
{
    public bool IsNonPlayableHousehold => !IsPlayableHousehold;

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
    private string _selectedHouseholdText = string.Empty;

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

    public string SelectedHouseholdText
    {
        get => _selectedHouseholdText;
        private set
        {
            if (_selectedHouseholdText == value) return;
            _selectedHouseholdText = value;
            OnPropertyChanged();
        }
    }

    public void Refresh()
    {
        SelectedHouseholdText = _main.GetFamilyRelationsActiveHouseholdText();
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
