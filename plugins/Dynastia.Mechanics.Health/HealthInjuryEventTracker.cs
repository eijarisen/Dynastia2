using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

internal sealed class HealthInjuryEventTracker
{
    private readonly IGameState _state;
    private readonly StandardHealthService _health;
    private readonly IGameRandom _random;

    public HealthInjuryEventTracker(IGameState state, StandardHealthService health, IGameRandom random, IGameEventBus events)
    {
        _state = state;
        _health = health;
        _random = random;
        events.EventPublished += OnEvent;
    }

    private void OnEvent(object? sender, GameEvent e)
    {
        if (!e.Type.StartsWith("rare.", StringComparison.OrdinalIgnoreCase)
            || !e.Data.TryGetValue("healthDamage", out var raw)
            || !double.TryParse(raw, out var damage)
            || damage < 10
            || e.SubjectId is not Guid id)
            return;

        var person = _state.People.FirstOrDefault(p => p.Id == id);
        if (person is null || person.Tags.Has("state.dead") || _random.NextDouble() >= 0.35)
            return;

        var condition = damage >= 25 && _random.NextDouble() < 0.04 ? "paraplegia"
            : damage >= 18 ? (_random.NextDouble() < 0.5 ? "broken_leg" : "concussion")
            : (_random.NextDouble() < 0.5 ? "broken_arm" : "concussion");
        _health.AddCondition(person, condition, e.Year);
    }
}
