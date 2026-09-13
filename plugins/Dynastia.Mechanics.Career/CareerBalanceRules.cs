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

    public static double GetWorkHarderPromotionBonus(
        int intellect,
        int educationLevel)
    {
        var intellectValue = Math.Clamp(intellect, 1, 5);
        var educationValue = Math.Clamp(educationLevel, 0, 5);

        return 0.12
            + intellectValue * 0.04
            + educationValue * 0.04;
    }
}
