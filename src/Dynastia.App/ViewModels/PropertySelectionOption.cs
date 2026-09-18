namespace Dynastia.App.ViewModels;

public sealed record PropertySelectionOption(
    string Id,
    string PrimaryText,
    string SecondaryText,
    string DetailsText,
    string PriceText,
    string SearchText,
    bool IsEnabled = true)
{
    public double DisplayOpacity =>
        IsEnabled ? 1.0 : 0.42;

    public bool HasDetailsText =>
        !string.IsNullOrWhiteSpace(DetailsText);
}

public sealed class ActionSelectionRequestedEventArgs : EventArgs
{
    public ActionSelectionRequestedEventArgs(string actionId)
    {
        ActionId = actionId;
    }

    public string ActionId { get; }
}
