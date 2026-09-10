using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class StandardHealthService : IHealthService
{
    public void EnsureHealth(IPerson person)
    {
        if (person.Components.Has<HealthComponent>())
            return;

        person.Components.Set(
            new HealthComponent
            {
                Current = 100,
                Maximum = 100
            });
    }

    public HealthSnapshot GetHealth(IPerson person)
    {
        var health = GetRequired(person);

        var conditions = health.Conditions
            .Select(condition => new HealthConditionInfo(
                condition.Id,
                condition.Name,
                condition.Type,
                condition.HealthImpact))
            .ToList();

        return new HealthSnapshot(
            health.Current,
            health.Maximum,
            conditions);
    }

    public void SetHealth(
        IPerson person,
        double value)
    {
        var health = GetRequired(person);

        health.Current = Math.Min(
            value,
            health.Maximum);
    }

    public void ChangeHealth(
        IPerson person,
        double amount)
    {
        var health = GetRequired(person);

        health.Current = Math.Min(
            health.Current + amount,
            health.Maximum);
    }

    private HealthComponent GetRequired(IPerson person)
    {
        EnsureHealth(person);

        return person.Components.Get<HealthComponent>()
            ?? throw new InvalidOperationException(
                "Health component could not be created.");
    }
}
