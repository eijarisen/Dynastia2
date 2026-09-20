namespace Dynastia.Mechanics.Career;

public static class CareerPressureRules
{
    public static double GetFailedRepeatedOverworkSatisfactionLossChance(
        int consecutiveWorkHarderYears) =>
        consecutiveWorkHarderYears switch
        {
            < 2 => 0.0,
            2 => 0.35,
            3 => 0.55,
            4 => 0.70,
            _ => 0.80
        };

    public static double GetLowSatisfactionStress(
        int jobSatisfaction) =>
        Math.Clamp(jobSatisfaction, 1, 5) switch
        {
            1 => 2.5,
            2 => 1.0,
            _ => 0.0
        };

    public static double GetRepeatedOverworkStress(
        int workHarderUsesInLastThreeYears) =>
        workHarderUsesInLastThreeYears switch
        {
            <= 0 => 0.0,
            1 => 0.5,
            2 => 1.5,
            _ => 2.5
        };
}
