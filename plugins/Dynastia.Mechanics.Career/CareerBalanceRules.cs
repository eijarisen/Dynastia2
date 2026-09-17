using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public static class CareerBalanceRules
{
    public static double GetCompositeAptitude(
        int primaryStat,
        int? secondaryStat = null)
    {
        var primary = Math.Clamp(primaryStat, 1, 5);
        if (secondaryStat is null)
            return primary;

        var secondary = Math.Clamp(secondaryStat.Value, 1, 5);
        return primary * 0.75 + secondary * 0.25;
    }

    public static double GetEmploymentSearchChance(
        double aptitudeStat,
        CareerOpportunityStrength opportunityStrength)
    {
        var aptitude = Math.Clamp(aptitudeStat, 1, 5);
        var lower = (int)Math.Floor(aptitude);
        var upper = (int)Math.Ceiling(aptitude);
        static double ChanceFor(int value) => value switch
        {
            1 => 0.35,
            2 => 0.50,
            3 => 0.65,
            4 => 0.80,
            _ => 0.90
        };
        var fraction = aptitude - lower;
        var baseChance = ChanceFor(lower)
            + (ChanceFor(upper) - ChanceFor(lower)) * fraction;

        baseChance += opportunityStrength switch
        {
            CareerOpportunityStrength.Regional => 0.05,
            CareerOpportunityStrength.Town => 0.08,
            _ => 0
        };

        return Math.Min(0.95, baseChance);
    }

    public static double GetPromotionAptitude(
        double careerAbility,
        int intellect,
        bool primaryIsIntellect,
        int currentJobLevel)
    {
        var ability = Math.Clamp(careerAbility, 1, 5);
        if (primaryIsIntellect || currentJobLevel <= 2)
            return ability;

        return Math.Clamp(
            ability * 0.60 + Math.Clamp(intellect, 1, 5) * 0.40,
            1,
            5);
    }

    public static double GetWorkHarderPromotionBonus(
        double aptitudeStat,
        int educationLevel)
    {
        var aptitudeValue = Math.Clamp(aptitudeStat, 1, 5);
        var educationValue = Math.Clamp(educationLevel, 0, 5);
        return 0.12 + aptitudeValue * 0.04 + educationValue * 0.04;
    }

    public static double GetTargetLevelPromotionMultiplier(
        int targetJobLevel) =>
        targetJobLevel switch
        {
            4 => 0.80,
            >= 5 => 0.35,
            _ => 1.0
        };

    public static double GetEducationPromotionMultiplier(
        int actualEducation,
        int expectedEducation)
    {
        var gap = Math.Max(0, expectedEducation - actualEducation);
        return gap switch
        {
            0 => 1.0,
            1 => 0.35,
            _ => 0.10
        };
    }
}
