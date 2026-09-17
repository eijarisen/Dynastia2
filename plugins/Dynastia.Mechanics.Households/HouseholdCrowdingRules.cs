namespace Dynastia.Mechanics.Households;

public static class HouseholdCrowdingRules
{
    public const int OvercrowdingThreshold = 8;
    public const double AnnualHealthPenalty = 3;
    public const double AnnualStressPenalty = 2;

    public static bool IsOvercrowded(int residentCount)
    {
        return residentCount > OvercrowdingThreshold;
    }
}
