namespace Dynastia.Mechanics.Wellbeing;

public static class TherapyRules
{
    public static double GetSuccessChance(int intellect) =>
        Math.Clamp(intellect, 1, 5) / 10.0;

    public static bool IsTreatableCondition(string conditionId) =>
        conditionId.Equals("alcoholism", StringComparison.OrdinalIgnoreCase)
        || conditionId.Equals("depression", StringComparison.OrdinalIgnoreCase)
        || conditionId.Equals("anxiety", StringComparison.OrdinalIgnoreCase)
        || conditionId.Equals("drug_dependence", StringComparison.OrdinalIgnoreCase)
        || conditionId.Equals("burnout", StringComparison.OrdinalIgnoreCase)
        || conditionId.Equals("gambling_disorder", StringComparison.OrdinalIgnoreCase);
}
