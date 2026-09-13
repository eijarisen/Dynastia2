namespace Dynastia.App.ViewModels;

public sealed record SelfImprovementOption(
    string ActionId,
    string StatName,
    string ActionLabel,
    int CurrentValue,
    decimal Cost,
    string Description,
    bool IsAvailable,
    string AvailabilityText)
{
    public string CurrentValueText =>
        $"{CurrentValue} / 5";

    public string CostText =>
        $"{Cost:N0} zł";
}
