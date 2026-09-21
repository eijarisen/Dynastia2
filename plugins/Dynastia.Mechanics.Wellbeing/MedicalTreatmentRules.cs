namespace Dynastia.Mechanics.Wellbeing;

public static class MedicalTreatmentRules
{
    public const double MaximumSuccessChance = 0.95;
    public const decimal VisitingPhysicianCostMultiplier = 1.50m;
    public const double VisitingPhysicianHealMultiplier = 0.85;

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

    public static double AdjustHealAmount(
        double baseHealAmount,
        double healMultiplier)
    {
        if (baseHealAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(baseHealAmount));
        if (healMultiplier < 0)
            throw new ArgumentOutOfRangeException(nameof(healMultiplier));

        return Math.Round(
            baseHealAmount * healMultiplier,
            1,
            MidpointRounding.AwayFromZero);
    }
}
