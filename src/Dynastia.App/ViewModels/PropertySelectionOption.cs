namespace Dynastia.App.ViewModels;

public sealed record PropertySelectionOption(
    string Id,
    string PrimaryText,
    string SecondaryText,
    string DetailsText,
    string PriceText,
    string SearchText);

public sealed class ActionSelectionRequestedEventArgs : EventArgs
{
    public ActionSelectionRequestedEventArgs(string actionId)
    {
        ActionId = actionId;
    }

    public string ActionId { get; }
}
