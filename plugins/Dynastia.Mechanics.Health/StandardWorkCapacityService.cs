using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class StandardWorkCapacityService : IWorkCapacityService
{
    private static readonly HashSet<string> IncapacitatingConditions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "appendicitis",
            "heart_attack",
            "stroke",
            "smallpox"
        };

    private readonly StandardHealthService _health;

    public StandardWorkCapacityService(StandardHealthService health)
    {
        _health = health;
    }

    public WorkCapacitySnapshot GetWorkCapacity(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (!person.Tags.Has("state.alive")
            || SimulationState.IsInactive(person))
        {
            return new WorkCapacitySnapshot(0, false);
        }

        // Generated adults can be queried by Career/Crafts synchronously while
        // New Game or relationship creation is still publishing its lifecycle
        // event, before the general reconciliation pass has created Health
        // state. Missing Health here means "not initialized yet", not illness;
        // use full work capacity until Health reconciliation runs.
        if (!person.Components.Has<HealthComponent>())
            return new WorkCapacitySnapshot(1.0, true);

        var snapshot = _health.GetHealth(person);
        if (snapshot.Current <= 10)
            return new WorkCapacitySnapshot(0, false, "health_critical");

        var healthMultiplier = snapshot.Percentage switch
        {
            < 20 => 0.35,
            < 35 => 0.60,
            < 50 => 0.75,
            < 65 => 0.90,
            _ => 1.00
        };

        var conditionMultiplier = 1.0;
        string? limitingCondition = null;

        foreach (var condition in snapshot.Conditions)
        {
            if (IncapacitatingConditions.Contains(condition.Id))
            {
                return new WorkCapacitySnapshot(
                    0,
                    false,
                    condition.Id);
            }

            var definition = _health.GetDefinition(condition.Id);
            if (definition is null)
                continue;

            var multiplier = GetConditionMultiplier(definition);
            if (multiplier < conditionMultiplier)
            {
                conditionMultiplier = multiplier;
                limitingCondition = condition.Id;
            }
        }

        var outputMultiplier = Math.Clamp(
            healthMultiplier * conditionMultiplier,
            0,
            1);

        return new WorkCapacitySnapshot(
            outputMultiplier,
            outputMultiplier > 0.001,
            limitingCondition);
    }

    private static double GetConditionMultiplier(
        HealthConditionDefinition definition)
    {
        var impact = Math.Abs(
            HealthSeverityRules.ScaleAnnualImpact(definition));

        if (definition.Category.Equals(
                "Serious",
                StringComparison.OrdinalIgnoreCase))
        {
            if (definition.Course.Equals(
                    "Terminal",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 0.55;
            }

            if (definition.Course.Equals(
                    "Acute",
                    StringComparison.OrdinalIgnoreCase))
            {
                return impact switch
                {
                    >= 15 => 0.35,
                    >= 12 => 0.50,
                    >= 8 => 0.65,
                    _ => 0.78
                };
            }

            return impact switch
            {
                >= 7 => 0.65,
                >= 5 => 0.72,
                >= 4 => 0.82,
                _ => 0.90
            };
        }

        if (definition.Category.Equals(
                "Injury",
                StringComparison.OrdinalIgnoreCase))
        {
            return impact switch
            {
                >= 7 => 0.35,
                >= 6 => 0.45,
                >= 5 => 0.58,
                _ => 0.75
            };
        }

        if (definition.Category.Equals(
                "Mental",
                StringComparison.OrdinalIgnoreCase))
        {
            return impact switch
            {
                >= 8 => 0.65,
                >= 5 => 0.75,
                >= 3 => 0.90,
                _ => 0.95
            };
        }

        if (definition.Category.Equals(
                "Birth",
                StringComparison.OrdinalIgnoreCase)
            && impact >= 2)
        {
            return 0.85;
        }

        return 1.0;
    }
}
