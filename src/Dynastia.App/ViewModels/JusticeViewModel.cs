using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class JusticeViewModel
{
    public JusticeViewModel(
        JusticeSnapshot snapshot)
    {
        IsImprisoned =
            snapshot.IsImprisoned;

        if (!snapshot.IsImprisoned)
        {
            StatusText = string.Empty;
            CrimeText = string.Empty;
            return;
        }

        var sentence =
            snapshot.IsLifeSentence
                ? "Life"
                : snapshot.RemainingYears == 1
                    ? "1 year left"
                    : $"{snapshot.RemainingYears} years left";

        StatusText =
            $"Prison: {sentence}";

        CrimeText =
            string.IsNullOrWhiteSpace(
                snapshot.CrimeName)
                ? string.Empty
                : string.IsNullOrWhiteSpace(
                    snapshot.CrimeDescription)
                    ? $"Conviction: {snapshot.CrimeName}"
                    : $"Conviction: {snapshot.CrimeName} — {snapshot.CrimeDescription}";
    }

    public bool IsImprisoned { get; }

    public string StatusText { get; }

    public string CrimeText { get; }
}
