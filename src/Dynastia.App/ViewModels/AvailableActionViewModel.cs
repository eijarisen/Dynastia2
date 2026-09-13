using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class AvailableActionViewModel
{
    private readonly Action _execute;

    public AvailableActionViewModel(
        GameActionDefinition definition,
        IReadOnlySet<ActionCategory> categories,
        Action execute)
    {
        Id =
            definition.Id;

        RawLabel =
            definition.Label;

        Label =
            ActionEmojiMap.Format(
                definition.Id,
                definition.Label);

        Description =
            definition.Description;

        Mode =
            definition.Mode;

        Categories = categories;

        _execute =
            execute;

        ExecuteCommand =
            new RelayCommand(
                _execute);
    }

    public string Id { get; }

    public string RawLabel { get; }

    public string Label { get; }

    public string Description { get; }

    public ActionExecutionMode Mode { get; }

    public IReadOnlySet<ActionCategory> Categories { get; }

    public string ModeText =>
        "Next year";

    public RelayCommand ExecuteCommand { get; }
}
