using Avalonia.Media;

namespace Dynastia.App.ViewModels;

public sealed record PropertySelectionOption(
    string Id,
    string PrimaryText,
    string SecondaryText,
    string DetailsText,
    string PriceText,
    string SearchText,
    bool IsEnabled = true,
    double? SuccessChance = null,
    string LeadingEmoji = "",
    double LeadingEmojiFontSize = 25)
{
    public double DisplayOpacity =>
        IsEnabled ? 1.0 : 0.42;

    public bool HasDetailsText =>
        !string.IsNullOrWhiteSpace(DetailsText);

    public bool HasSuccessChance =>
        SuccessChance.HasValue;

    public bool HasLeadingEmoji =>
        !string.IsNullOrWhiteSpace(LeadingEmoji);

    public string SuccessChanceText =>
        SuccessChance is { } chance
            ? $"Chance: {chance:P0}"
            : string.Empty;

    public IBrush SuccessChanceBrush =>
        ChancePresentation.ForProbability(
            SuccessChance ?? 0);
}

public sealed class ActionSelectionRequestedEventArgs : EventArgs
{
    public ActionSelectionRequestedEventArgs(string actionId)
    {
        ActionId = actionId;
    }

    public string ActionId { get; }
}
