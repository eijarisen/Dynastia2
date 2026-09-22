using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Childhood;

internal sealed class ChildhoodBalanceRules
{
    private const string Path = "Childhood/childhood_balance.json";

    public double StableCareRecoveryChance { get; init; }
    public double StableCareHealthMinimum { get; init; }
    public int StableCareTarget { get; init; }
    public int MajorTraumaRecoveryBlockYears { get; init; }

    public static ChildhoodBalanceRules Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var document = JsonDocument.Parse(data.ReadText(Path));
        var root = document.RootElement;
        var rules = new ChildhoodBalanceRules
        {
            StableCareRecoveryChance = root.GetProperty("stableCareRecoveryChance").GetDouble(),
            StableCareHealthMinimum = root.GetProperty("stableCareHealthMinimum").GetDouble(),
            StableCareTarget = root.GetProperty("stableCareTarget").GetInt32(),
            MajorTraumaRecoveryBlockYears = root.GetProperty("majorTraumaRecoveryBlockYears").GetInt32()
        };

        if (rules.StableCareRecoveryChance is < 0d or > 1d
            || rules.StableCareHealthMinimum is < 0d or > 100d
            || rules.StableCareTarget is < 1 or > 5
            || rules.MajorTraumaRecoveryBlockYears < 0)
        {
            throw new InvalidDataException("Childhood balance data contains an invalid stable-care rule.");
        }

        return rules;
    }
}
