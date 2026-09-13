namespace Dynastia.Mechanics.Reproduction;

public static class BirthConditionRules
{
    public const double FrequencyScale = 0.40;

    public static double GetLongevityModifier(int longevity) =>
        Math.Clamp(longevity, 1, 5) switch
        {
            1 => 1.20,
            2 => 1.10,
            3 => 1.00,
            4 => 0.90,
            _ => 0.80
        };

    public static double GetProbabilityScale(int longevity) =>
        FrequencyScale * GetLongevityModifier(longevity);

    public static BirthConditionDefinition? SelectCondition(
        IReadOnlyList<BirthConditionDefinition> definitions,
        double roll,
        int longevity)
    {
        var cumulative = 0.0;
        var probabilityScale = GetProbabilityScale(longevity);

        foreach (var definition in definitions)
        {
            cumulative += definition.Probability * probabilityScale;
            if (roll < cumulative)
                return definition;
        }

        return null;
    }
}
