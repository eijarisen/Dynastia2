namespace Dynastia.App.ViewModels;

public sealed class PlayablePersonTabViewModel
{
    public PlayablePersonTabViewModel(
        Guid personId,
        string generationText,
        string label,
        string fullName,
        string actionSummaryText,
        bool isActive,
        bool hasQueuedAction,
        Action<Guid> switchHousehold)
    {
        PersonId = personId;
        GenerationText = generationText;
        Label = label;
        FullName = fullName;
        ActionSummaryText = actionSummaryText;
        IsActive = isActive;
        HasQueuedAction = hasQueuedAction;

        SwitchCommand =
            new RelayCommand(
                () => switchHousehold(PersonId));
    }

    public Guid PersonId { get; }

    public string GenerationText { get; }

    public string Label { get; }

    public string FullName { get; }

    public string ActionSummaryText { get; }

    public bool IsActive { get; }

    public bool HasQueuedAction { get; }

    public RelayCommand SwitchCommand { get; }
}
