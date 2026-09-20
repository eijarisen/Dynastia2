namespace Dynastia.Mechanics.Households;

public static class HouseholdCrowdingRules
{
    public const int OvercrowdingThreshold = 8;
    public const double HealthPenaltyPerExcessResident = 0.75;
    public const double StressPenaltyPerExcessResident = 1.0;

    public static bool IsOvercrowded(int residentCount) =>
        GetResidentsAboveCapacity(residentCount) > 0;

    public static int GetResidentsAboveCapacity(int residentCount) =>
        Math.Max(0, residentCount - OvercrowdingThreshold);

    public static double GetAnnualHealthPenalty(int residentCount) =>
        GetResidentsAboveCapacity(residentCount)
        * HealthPenaltyPerExcessResident;

    public static double GetAnnualStressPenalty(int residentCount) =>
        GetResidentsAboveCapacity(residentCount)
        * StressPenaltyPerExcessResident;
}
