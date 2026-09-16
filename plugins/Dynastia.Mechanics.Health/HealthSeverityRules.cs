namespace Dynastia.Mechanics.Health;

/// <summary>
/// Global severity tuning for health conditions. Incidence is balanced
/// separately in <see cref="HealthIncidenceRules"/> so frequency can remain
/// stable while individual illnesses have a more meaningful health cost.
/// </summary>
public static class HealthSeverityRules
{
    public const double MildAnnualMultiplier = 1.35;
    public const double SeriousAnnualMultiplier = 1.40;
    public const double MentalAnnualMultiplier = 1.30;
    public const double InjuryAnnualMultiplier = 1.30;
    public const double SeriousImmediateMultiplier = 1.25;
    public const double InjuryImmediateMultiplier = 1.20;

    public static double ScaleAnnualImpact(
        HealthConditionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.HealthImpact >= 0)
            return definition.HealthImpact;

        var multiplier = definition.Category switch
        {
            var category when category.Equals(
                "Mild",
                StringComparison.OrdinalIgnoreCase) =>
                MildAnnualMultiplier,

            var category when category.Equals(
                "Serious",
                StringComparison.OrdinalIgnoreCase) =>
                SeriousAnnualMultiplier,

            var category when category.Equals(
                "Mental",
                StringComparison.OrdinalIgnoreCase) =>
                MentalAnnualMultiplier,

            var category when category.Equals(
                "Injury",
                StringComparison.OrdinalIgnoreCase) =>
                InjuryAnnualMultiplier,

            _ => 1.0
        };

        return definition.HealthImpact * multiplier;
    }

    public static double ScaleImmediateImpact(
        HealthConditionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.ImmediateHealthImpact >= 0)
            return definition.ImmediateHealthImpact;

        var multiplier = definition.Category switch
        {
            var category when category.Equals(
                "Serious",
                StringComparison.OrdinalIgnoreCase) =>
                SeriousImmediateMultiplier,

            var category when category.Equals(
                "Injury",
                StringComparison.OrdinalIgnoreCase) =>
                InjuryImmediateMultiplier,

            _ => 1.0
        };

        return definition.ImmediateHealthImpact * multiplier;
    }
}
