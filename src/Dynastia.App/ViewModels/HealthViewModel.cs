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
                    snapshot.Conditions.Select(x => x.Name));
    }

    public double Current { get; }
    public double Maximum { get; }
    public double Percentage { get; }

    public string HealthText =>
        $"Health: {Math.Round(Current)}/{Math.Round(Maximum)}";

    public string ConditionsText { get; }
}
