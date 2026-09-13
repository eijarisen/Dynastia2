namespace Dynastia.Mechanics.Health;

public static class HealthIncidenceRules
{
    public const double MildConditionFrequencyScale = 0.45;
    public const double SeriousConditionFrequencyScale = 0.35;

    public static double ScaleMildConditionChance(double chance) =>
        Math.Clamp(chance * MildConditionFrequencyScale, 0, 1);

    public static double ScaleSeriousConditionChance(double chance) =>
        Math.Clamp(chance * SeriousConditionFrequencyScale, 0, 1);
}
