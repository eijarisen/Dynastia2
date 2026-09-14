namespace Dynastia.Mechanics.Education;

public static class EducationProgressionRules
{
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
