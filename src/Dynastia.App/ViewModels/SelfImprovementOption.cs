namespace Dynastia.App.ViewModels;

public sealed record SelfImprovementOption(
    string ActionId,
    string StatName,
    string ActionLabel,
    int? CurrentValue,
    decimal Cost,
    string Description,
    bool IsAvailable,
    string AvailabilityText)
{
    public bool HasCurrentValue => CurrentValue is not null;

    public string CurrentValueText =>
        CurrentValue is int value
            ? $"{value} / 5"
            : string.Empty;

    public string CostText =>
        $"{Cost:N0} zł";
}
