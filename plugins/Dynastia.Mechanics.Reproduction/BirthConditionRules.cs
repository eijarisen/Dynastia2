namespace Dynastia.Mechanics.Reproduction;

public static class BirthConditionRules
{
    public const double FrequencyScale = 0.40;

    // Kept as compatibility helpers for older tests/callers. Longevity no
    // longer changes congenital-condition probability.
    public static double GetLongevityModifier(int longevity) => 1.0;
    public static double GetProbabilityScale(int longevity) => FrequencyScale;

    public static BirthConditionDefinition? SelectCondition(
        IReadOnlyList<BirthConditionDefinition> definitions,
        double roll,
        int longevity) =>
        SelectCondition(definitions, roll, year: 1700, motherAge: 30, context: null);

    public static BirthConditionDefinition? SelectCondition(
        IReadOnlyList<BirthConditionDefinition> definitions,
        double roll,
        int year,
        int motherAge,
        BirthConditionContextCatalog? context)
    {
        var cumulative = 0.0;

        foreach (var definition in definitions)
        {
            if (year < definition.StartYear
                || definition.EndYear is int endYear && year > endYear)
            {
                continue;
            }

            var multiplier = context?.GetMultiplier(definition.Id, year, motherAge) ?? 1.0;
            cumulative += definition.Probability * FrequencyScale * multiplier;
            if (roll < cumulative)
                return definition;
        }

        return null;
    }
}
