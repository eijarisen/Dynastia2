using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class AvailableActionViewModel
{
    private readonly Action _execute;

    public AvailableActionViewModel(
        GameActionDefinition definition,
        Action execute)
    {
        Id = definition.Id;
        Label = definition.Label;
        Description = definition.Description;
        Mode = definition.Mode;
        _execute = execute;

        ExecuteCommand = new RelayCommand(_execute);
    }

    public string Id { get; }
    public string Label { get; }
    public string Description { get; }
    public ActionExecutionMode Mode { get; }

    public string ModeText =>
        Mode == ActionExecutionMode.Queued
            ? "Next year"
            : "Immediate";

    public RelayCommand ExecuteCommand { get; }
}
