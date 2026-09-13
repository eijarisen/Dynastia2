namespace Dynastia.Mechanics.Education;

public static class EducationProgressionRules
{
    public static int GetPassiveChildhoodCeiling(
        int intellect) =>
        Math.Clamp(intellect, 1, 5);

    public static int GetHelpedChildhoodCeiling(
        int intellect) =>
        Math.Min(
            5,
            Math.Clamp(intellect, 1, 5) + 1);
}
