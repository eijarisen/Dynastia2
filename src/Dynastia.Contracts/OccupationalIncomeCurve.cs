namespace Dynastia.Contracts;

public static class OccupationalIncomeCurve
{
    public const decimal TailCap = 20m;

    public static decimal GetLegacyMultiplier(
        int roll,
        int masteryLevel,
        int minimumRoll = 0,
        int maximumRollInclusive = 94)
    {
        ValidateRange(minimumRoll, maximumRollInclusive);
        var mastery = Math.Clamp(masteryLevel, 1, 5);
        var clampedRoll = Math.Clamp(roll, minimumRoll, maximumRollInclusive);
        var denominator = Math.Max(
            1,
            Math.Min(
                99,
                100 - clampedRoll - mastery));
        return 100m / denominator;
    }

    public static decimal GetMultiplier(
        int roll,
        int masteryLevel,
        int minimumRoll = 0,
        int maximumRollInclusive = 94)
    {
        ValidateRange(minimumRoll, maximumRollInclusive);

        var scale = GetScale(
            masteryLevel,
            minimumRoll,
            maximumRollInclusive);
        var legacy = GetLegacyMultiplier(
            roll,
            masteryLevel,
            minimumRoll,
            maximumRollInclusive);

        return Math.Min(legacy, TailCap) * scale;
    }

    public static decimal GetExpectedMultiplier(
        int masteryLevel,
        int minimumRoll = 0,
        int maximumRollInclusive = 94)
    {
        ValidateRange(minimumRoll, maximumRollInclusive);
        decimal total = 0m;
        var count = maximumRollInclusive - minimumRoll + 1;
        for (var roll = minimumRoll; roll <= maximumRollInclusive; roll++)
        {
            total += GetMultiplier(
                roll,
                masteryLevel,
                minimumRoll,
                maximumRollInclusive);
        }

        return total / count;
    }

    public static decimal GetMedianMultiplier(
        int masteryLevel,
        int minimumRoll = 0,
        int maximumRollInclusive = 94)
    {
        ValidateRange(minimumRoll, maximumRollInclusive);
        var middleRoll = minimumRoll + (maximumRollInclusive - minimumRoll) / 2;
        return GetMultiplier(
            middleRoll,
            masteryLevel,
            minimumRoll,
            maximumRollInclusive);
    }

    public static decimal GetMaximumMultiplier(
        int masteryLevel,
        int minimumRoll = 0,
        int maximumRollInclusive = 94) =>
        GetMultiplier(
            maximumRollInclusive,
            masteryLevel,
            minimumRoll,
            maximumRollInclusive);

    private static decimal GetScale(
        int masteryLevel,
        int minimumRoll,
        int maximumRollInclusive)
    {
        decimal legacyTotal = 0m;
        decimal cappedTotal = 0m;
        for (var roll = minimumRoll; roll <= maximumRollInclusive; roll++)
        {
            var legacy = GetLegacyMultiplier(
                roll,
                masteryLevel,
                minimumRoll,
                maximumRollInclusive);
            legacyTotal += legacy;
            cappedTotal += Math.Min(legacy, TailCap);
        }

        return cappedTotal <= 0m
            ? 1m
            : legacyTotal / cappedTotal;
    }

    private static void ValidateRange(int minimumRoll, int maximumRollInclusive)
    {
        if (maximumRollInclusive < minimumRoll)
            throw new ArgumentOutOfRangeException(nameof(maximumRollInclusive));
    }
}
