using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

internal sealed class HealthInjuryEventTracker
{
    private static readonly (string Id, double Weight)[] PermanentInjuries =
    [
        ("chronic_pain", 40),
        ("mobility_impairment", 25),
        ("hearing_loss", 15),
        ("traumatic_brain_injury", 15),
        ("paraplegia", 5)
    ];

    private readonly IGameState _state;
    private readonly StandardHealthService _health;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public HealthInjuryEventTracker(IGameState state, StandardHealthService health, IGameRandom random, IGameEventBus events)
    {
        _state = state;
        _health = health;
        _random = random;
        _events = events;
        events.EventPublished += OnEvent;
    }

    private void OnEvent(object? sender, GameEvent e)
    {
        if (e.Type.Equals("health.injury_exposure", StringComparison.OrdinalIgnoreCase))
        {
            ResolvePermanentInjury(e);
            return;
        }

        // Preserve the existing acute-injury behavior for ordinary Rare Events.
        if (!e.Type.StartsWith("rare.", StringComparison.OrdinalIgnoreCase)
            || !e.Data.TryGetValue("healthDamage", out var raw)
            || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var damage)
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

    private void ResolvePermanentInjury(GameEvent e)
    {
        if (!e.Data.TryGetValue("permanentRisk", out var risk)
            || !risk.Equals("true", StringComparison.OrdinalIgnoreCase)
            || !e.Data.TryGetValue("healthDamage", out var rawDamage)
            || !double.TryParse(rawDamage, NumberStyles.Float, CultureInfo.InvariantCulture, out var damage)
            || e.SubjectId is not Guid id)
        {
            return;
        }

        var person = _state.People.FirstOrDefault(p => p.Id == id);
        if (person is null || !person.Tags.Has("state.alive"))
            return;

        var chance = GetPermanentInjuryChance(damage);
        if (_random.NextDouble() >= chance)
            return;

        var candidates = PermanentInjuries
            .Where(candidate => !_health.HasCondition(person, candidate.Id))
            .ToList();
        if (candidates.Count == 0)
            return;

        var totalWeight = candidates.Sum(candidate => candidate.Weight);
        var roll = _random.NextDouble() * totalWeight;
        var selected = candidates[^1].Id;
        foreach (var candidate in candidates)
        {
            if (roll < candidate.Weight)
            {
                selected = candidate.Id;
                break;
            }
            roll -= candidate.Weight;
        }

        if (!_health.AddCondition(person, selected, e.Year))
            return;

        var definition = _health.GetDefinition(selected);
        var conditionName = definition?.Name ?? selected;
        var sourceId = e.Data.TryGetValue("sourceId", out var rawSourceId)
            ? rawSourceId
            : "a severe event";
        var sourceCategory = e.Data.TryGetValue("sourceCategory", out var rawSourceCategory)
            ? rawSourceCategory
            : "accident";

        _events.Publish(new GameEvent
        {
            Type = "health.permanent_injury",
            Year = e.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["conditionId"] = selected,
                ["condition"] = conditionName,
                ["sourceId"] = sourceId,
                ["sourceCategory"] = sourceCategory,
                ["familyNews"] = (definition?.Newsworthy == true).ToString(),
                ["text"] = $"{person.Name} {person.Surname} was left with {conditionName} after severe injuries."
            }
        });
    }

    internal static double GetPermanentInjuryChance(double damage) =>
        damage < 8 ? 0.08
        : damage <= 12 ? 0.12
        : 0.18;
}
