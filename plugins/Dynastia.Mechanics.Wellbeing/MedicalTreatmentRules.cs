namespace Dynastia.Mechanics.Wellbeing;

public static class MedicalTreatmentRules
{
    public const double MaximumSuccessChance = 0.95;

    public static decimal AdjustCost(
        decimal baseCost,
        decimal costMultiplier)
    {
        if (baseCost < 0)
            throw new ArgumentOutOfRangeException(nameof(baseCost));
        if (costMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(costMultiplier));

        return Math.Round(
            baseCost * costMultiplier,
            0,
            MidpointRounding.AwayFromZero);
    }

    public static double AdjustSuccessChance(
        double baseChance,
        double successAdd) =>
        Math.Clamp(
            baseChance + successAdd,
            0,
            MaximumSuccessChance);
}
