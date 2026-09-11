using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class HealthViewModel
{
    public HealthViewModel(HealthSnapshot snapshot)
    {
        Current = snapshot.Current;
        Maximum = snapshot.Maximum;
        Percentage = snapshot.Percentage;

        ConditionsText =
            snapshot.Conditions.Count == 0
                ? "No health conditions"
                : string.Join(
                    ", ",
                    snapshot.Conditions.Select(FormatCondition));
    }

    public double Current { get; }
    public double Maximum { get; }
    public double Percentage { get; }

    public string HealthText =>
        $"Health: {Current:0.#}/{Maximum:0.#}";

    public string HealthSummaryText =>
        $"{HealthText} • {ConditionsText}";

    public string ConditionsText { get; }

    private static string FormatCondition(
        HealthConditionInfo condition)
    {
        if (condition.RemainingYears is int years
            && !condition.Type.Equals(
                "terminal",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"{condition.Name} ({years}y)";
        }

        return condition.Name;
    }
}
