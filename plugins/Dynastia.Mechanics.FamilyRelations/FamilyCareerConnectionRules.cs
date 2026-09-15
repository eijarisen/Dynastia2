namespace Dynastia.Mechanics.FamilyRelations;

public static class FamilyCareerConnectionRules
{
    public const double ExceptionalPlacementChance =
        0.15;

    public static bool CanProvideHelp(
        int highestJobLevel) =>
        highestJobLevel >= 2;

    public static int GetStandardPlacementLevel(
        int highestJobLevel)
    {
        if (!CanProvideHelp(highestJobLevel))
            return 0;

        return Math.Clamp(
            highestJobLevel - 2,
            1,
            3);
    }

    public static int GetExceptionalPlacementLevel(
        int highestJobLevel)
    {
        if (!CanProvideHelp(highestJobLevel))
            return 0;

        // A level-2 contact can occasionally pull an existing level-1
        // worker up alongside them. Stronger contacts still top out one
        // level below their own position, preventing reciprocal pumping.
        if (highestJobLevel == 2)
            return 2;

        return Math.Clamp(
            highestJobLevel - 1,
            1,
            4);
    }

    public static bool CanImprove(
        int currentJobLevel,
        int highestJobLevel) =>
        currentJobLevel
        < GetExceptionalPlacementLevel(
            highestJobLevel);
}
