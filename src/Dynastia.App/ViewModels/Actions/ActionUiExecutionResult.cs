namespace Dynastia.App.ViewModels.Actions;

/// <summary>
/// An attempt may have changed state even when rejected. MainWindow owns the refresh and
/// status message policy; selection submissions historically show failures, ordinary actions do not.
/// </summary>
internal sealed record ActionUiExecutionResult(
    bool Attempted,
    bool Success,
    string? Message,
    bool StateMayHaveChanged,
    bool FromSelection = false)
{
    internal static readonly ActionUiExecutionResult NotAttempted = new(false, false, null, false);
}
