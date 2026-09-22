using System.Globalization;

namespace Dynastia.Contracts;

public sealed record AnnualProductiveEffortSnapshot(
    double WorkCapacityMultiplier,
    decimal RecoverReductionPercent,
    double OutputMultiplier,
    bool CanProduce)
{
    public decimal RecoverMultiplier =>
        1m - RecoverReductionPercent / 100m;

    public decimal Apply(decimal output) =>
        output <= 0m || !CanProduce
            ? 0m
            : output * (decimal)OutputMultiplier;
}

public static class AnnualProductiveEffortRules
{
    public const string RecoverTagPrefix =
        "modifier.salary.recover.";

    public static AnnualProductiveEffortSnapshot Get(
        IPerson person,
        IWorkCapacityService workCapacity)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(workCapacity);

        var capacity = workCapacity.GetWorkCapacity(person);
        var capacityMultiplier = Math.Clamp(
            capacity.OutputMultiplier,
            0,
            1);
        var recoverReduction = ReadRecoverReductionPercent(person);
        var recoverMultiplier = 1.0 - (double)(recoverReduction / 100m);
        var outputMultiplier = Math.Clamp(
            capacityMultiplier * recoverMultiplier,
            0,
            1);

        return new AnnualProductiveEffortSnapshot(
            capacityMultiplier,
            recoverReduction,
            outputMultiplier,
            capacity.CanWork && outputMultiplier > 0.001);
    }

    public static decimal ReadRecoverReductionPercent(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        foreach (var tag in person.Tags.All)
        {
            if (!tag.StartsWith(
                    RecoverTagPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (decimal.TryParse(
                    tag[RecoverTagPrefix.Length..],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var percent))
            {
                return Math.Clamp(percent, 0m, 50m);
            }
        }

        return 0m;
    }
}
