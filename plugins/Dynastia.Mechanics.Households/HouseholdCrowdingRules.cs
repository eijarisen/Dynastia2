namespace Dynastia.Mechanics.Households;

public static class HouseholdCrowdingRules
{
    public const int OvercrowdingThreshold = 8;
    public const double HealthPenaltyPerExcessResident = 0.75;
    public const double StressPenaltyPerExcessResident = 1.0;

    public static bool IsOvercrowded(
        int residentCount,
        int residentCapacity = OvercrowdingThreshold) =>
        GetResidentsAboveCapacity(residentCount, residentCapacity) > 0;

    public static int GetResidentsAboveCapacity(
        int residentCount,
        int residentCapacity = OvercrowdingThreshold) =>
        Math.Max(0, residentCount - Math.Max(1, residentCapacity));

    public static double GetAnnualHealthPenalty(
        int residentCount,
        int residentCapacity = OvercrowdingThreshold) =>
        GetResidentsAboveCapacity(residentCount, residentCapacity)
        * HealthPenaltyPerExcessResident;

    public static double GetAnnualStressPenalty(
        int residentCount,
        int residentCapacity = OvercrowdingThreshold) =>
        GetResidentsAboveCapacity(residentCount, residentCapacity)
        * StressPenaltyPerExcessResident;
}
