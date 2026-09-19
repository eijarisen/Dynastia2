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

    public static double GetHelpInEducationSuccessChance(
        int childIntellect,
        int helperIntellect) =>
        Math.Clamp(
            0.10
            + Math.Clamp(childIntellect, 1, 5) * 0.05
            + Math.Clamp(helperIntellect, 1, 5) * 0.10,
            0,
            1);

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
