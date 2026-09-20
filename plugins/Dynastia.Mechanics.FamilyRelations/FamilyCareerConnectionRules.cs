namespace Dynastia.Mechanics.FamilyRelations;

public static class FamilyCareerConnectionRules
{
    public static bool CanProvideHelp(
        int highestJobLevel) =>
        highestJobLevel >= 3;

    public static int GetPlacementLevel(
        int highestJobLevel)
    {
        if (!CanProvideHelp(highestJobLevel))
            return 0;

        // Nepotism remains powerful, but can never put the recipient closer
        // than two levels below the strongest helper. Repeated use therefore
        // cannot bootstrap a household upward past the same fixed ceiling.
        return Math.Clamp(
            highestJobLevel - 2,
            1,
            3);
    }

    public static int GetStandardPlacementLevel(
        int highestJobLevel) =>
        GetPlacementLevel(highestJobLevel);

    public static int GetExceptionalPlacementLevel(
        int highestJobLevel) =>
        GetPlacementLevel(highestJobLevel);

    public static bool CanImprove(
        int currentJobLevel,
        int highestJobLevel) =>
        currentJobLevel < GetPlacementLevel(highestJobLevel);
}
