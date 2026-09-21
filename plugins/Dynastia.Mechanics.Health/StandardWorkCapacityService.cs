using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class StandardWorkCapacityService : IWorkCapacityService
{
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

        // Generated adults can be queried synchronously before Health
        // reconciliation has created their component. Missing Health means
        // "not initialized yet", not incapacity, and this read must not mutate it.
        if (!person.Components.Has<HealthComponent>())
            return new WorkCapacitySnapshot(1.0, true);

        var snapshot = _health.GetHealth(person);
        if (snapshot.Current <= 10)
            return new WorkCapacitySnapshot(0, false, "health_critical");

        var outputMultiplier = 1.0;
        string? limitingCondition = null;

        foreach (var condition in snapshot.Conditions)
        {
            var definition = _health.GetDefinition(condition.Id);
            if (definition is null)
                continue;

            var multiplier = Math.Clamp(
                definition.WorkCapacityMultiplier,
                0,
                1);
            if (multiplier < outputMultiplier)
            {
                outputMultiplier = multiplier;
                limitingCondition = condition.Id;
            }
        }

        outputMultiplier = Math.Clamp(outputMultiplier, 0, 1);
        return new WorkCapacitySnapshot(
            outputMultiplier,
            outputMultiplier > 0.001,
            limitingCondition);
    }
}
