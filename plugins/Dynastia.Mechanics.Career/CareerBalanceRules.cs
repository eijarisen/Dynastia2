using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public static class CareerBalanceRules
{
    public static double GetEmploymentSearchChance(
        int aptitudeStat,
        CareerOpportunityStrength opportunityStrength)
    {
        var baseChance = Math.Clamp(aptitudeStat, 1, 5) switch
        {
            1 => 0.35,
            2 => 0.50,
            3 => 0.65,
            4 => 0.80,
            _ => 0.90
        };

        baseChance += opportunityStrength switch
        {
            CareerOpportunityStrength.Regional => 0.05,
            CareerOpportunityStrength.Town => 0.08,
            _ => 0
        };

        return Math.Min(0.95, baseChance);
    }

    public static int GetPromotionAptitudeStat(
        bool strengthDrivenCareer,
        int currentJobLevel,
        int strength,
        int intellect)
    {
        var useStrength =
            strengthDrivenCareer
            && currentJobLevel <= 2;

        return Math.Clamp(
            useStrength
                ? strength
                : intellect,
            1,
            5);
    }

    public static bool PromotionUsesEducation(
        bool strengthDrivenCareer,
        int currentJobLevel) =>
        !strengthDrivenCareer
        || currentJobLevel >= 3;

    public static double GetWorkHarderPromotionBonus(
        int aptitudeStat,
        int educationLevel,
        bool educationMatters = true)
    {
        var aptitudeValue =
            Math.Clamp(aptitudeStat, 1, 5);

        var educationValue =
            educationMatters
                ? Math.Clamp(educationLevel, 0, 5)
                : 0;

        return 0.12
            + aptitudeValue * 0.04
            + educationValue * 0.04;
    }
}
