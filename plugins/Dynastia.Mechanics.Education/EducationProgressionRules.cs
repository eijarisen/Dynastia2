namespace Dynastia.Mechanics.Education;

public static class EducationProgressionRules
{
    public static double GetPaidEducationSuccessChance(
        int intellect) =>
        Math.Clamp(intellect, 1, 5) switch
        {
            1 => 0.05,
            2 => 0.15,
            3 => 0.30,
            4 => 0.50,
            _ => 0.70
        };

    public static int GetPassiveChildhoodCeiling(
        int intellect,
        int eraMaximum = 5) =>
        Math.Min(
            Math.Clamp(intellect, 1, 5),
            Math.Clamp(eraMaximum, 0, 5));

    public static int GetHelpedChildhoodCeiling(
        int intellect,
        int eraMaximum = 5) =>
        Math.Min(
            Math.Min(
                5,
                Math.Clamp(intellect, 1, 5) + 1),
            Math.Clamp(eraMaximum, 0, 5));
    public static double ApplyPassiveChanceMultiplier(
        double chance,
        EducationEraRule era) =>
        Math.Clamp(
            chance * era.PassiveChanceMultiplier,
            0,
            1);

}
