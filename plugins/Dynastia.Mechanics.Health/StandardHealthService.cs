using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class StandardHealthService : IHealthService
{
    private const string ConditionsPath = "Common/health_conditions.json";
    private readonly IGameRandom _random;
    private readonly Dictionary<string, HealthConditionDefinition> _definitions;

    public StandardHealthService(IGameDataService data, IGameRandom random)
    {
        _random = random;
        var definitions = JsonSerializer.Deserialize<List<HealthConditionDefinition>>(
            data.ReadText(ConditionsPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Could not read {ConditionsPath}.");
        ValidateDefinitions(definitions);
        _definitions = definitions.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    }

    public void EnsureHealth(IPerson person)
    {
        if (!person.Components.Has<HealthComponent>())
            person.Components.Set(new HealthComponent { Current = 100, Maximum = 100 });
    }

    public HealthSnapshot GetHealth(IPerson person)
    {
        var health = GetRequired(person);
        return new HealthSnapshot(
            health.Current,
            health.Maximum,
            health.Conditions.Select(c => new HealthConditionInfo(c.Id, c.Name, c.Type, c.HealthImpact, c.RemainingYears)).ToList());
    }

    public void SetHealth(IPerson person, double value) => GetRequired(person).Current = Math.Min(value, GetRequired(person).Maximum);
    public void ChangeHealth(IPerson person, double amount) => SetHealth(person, GetRequired(person).Current + amount);
    public void ChangeHealthUnclamped(IPerson person, double amount) => GetRequired(person).Current += amount;
    public bool HasCondition(IPerson person, string conditionId) => GetRequired(person).Conditions.Any(x => x.Id.Equals(conditionId, StringComparison.OrdinalIgnoreCase));
    public bool AddCondition(IPerson person, string conditionId) => TryAddCondition(person, conditionId, out _);
    public bool RemoveCondition(IPerson person, string conditionId) => GetRequired(person).Conditions.RemoveAll(x => x.Id.Equals(conditionId, StringComparison.OrdinalIgnoreCase)) > 0;

    internal HealthConditionDefinition? GetDefinition(string conditionId) => _definitions.TryGetValue(conditionId, out var d) ? d : null;
    internal bool IsFamilyNewsCondition(string conditionId) => GetDefinition(conditionId)?.Newsworthy == true || GetDefinition(conditionId)?.FamilyNews == true;

    internal double ApplyAnnualConditionEffects(IPerson person)
    {
        var health = GetRequired(person);
        var change = 0.0;
        foreach (var condition in health.Conditions)
        {
            var definition = GetDefinition(condition.Id);
            if (definition is not null
                && !IsPersistent(definition, condition)
                && !condition.RemainingYears.HasValue
                && definition.DurationMin.HasValue
                && definition.DurationMax.HasValue)
            {
                // Save migration for conditions that used to be permanent
                // (notably Anxiety/Depression) but now have a finite course.
                condition.RemainingYears = _random.NextInt(
                    definition.DurationMin.Value,
                    definition.DurationMax.Value);
            }

            change += condition.HealthImpact;
            if (condition.RemainingYears.HasValue && !IsPersistent(definition, condition))
                condition.RemainingYears--;
        }
        health.Conditions.RemoveAll(condition => !ShouldRetain(condition));
        return change;
    }

    internal bool TryAddWeightedCondition(
        IPerson person,
        string category,
        int age,
        Func<HealthConditionDefinition, double>? weightModifier,
        out HealthConditionState? added,
        out HealthConditionDefinition? selected)
    {
        var candidates = _definitions.Values
            .Where(d => d.Category.Equals(category, StringComparison.OrdinalIgnoreCase)
                        && age >= d.MinimumAge
                        && d.Weight > 0
                        && !HasCondition(person, d.Id))
            .Select(d => new { Definition = d, Weight = d.Weight * Math.Max(0, weightModifier?.Invoke(d) ?? 1) })
            .Where(x => x.Weight > 0)
            .ToList();

        if (candidates.Count == 0)
        {
            added = null;
            selected = null;
            return false;
        }

        var total = candidates.Sum(x => x.Weight);
        var roll = _random.NextDouble() * total;
        selected = candidates[^1].Definition;
        foreach (var entry in candidates)
        {
            if (roll < entry.Weight) { selected = entry.Definition; break; }
            roll -= entry.Weight;
        }

        return TryAddCondition(person, selected.Id, out added);
    }

    internal void ApplyImmediateImpact(IPerson person, HealthConditionDefinition definition)
    {
        if (definition.ImmediateHealthImpact != 0)
            ChangeHealth(person, definition.ImmediateHealthImpact);
    }

    private bool TryAddCondition(IPerson person, string conditionId, out HealthConditionState? added)
    {
        var health = GetRequired(person);
        if (health.Conditions.Any(x => x.Id.Equals(conditionId, StringComparison.OrdinalIgnoreCase)))
        {
            added = null;
            return false;
        }
        if (!_definitions.TryGetValue(conditionId, out var definition))
            throw new KeyNotFoundException($"Unknown health condition '{conditionId}'.");

        int? remaining = null;
        if (definition.DurationMin.HasValue && definition.DurationMax.HasValue)
            remaining = _random.NextInt(definition.DurationMin.Value, definition.DurationMax.Value);

        added = new HealthConditionState
        {
            Id = definition.Id,
            Name = definition.Name,
            Type = definition.Type,
            HealthImpact = definition.HealthImpact,
            RemainingYears = remaining
        };
        health.Conditions.Add(added);
        return true;
    }

    private HealthComponent GetRequired(IPerson person)
    {
        EnsureHealth(person);
        return person.Components.Get<HealthComponent>() ?? throw new InvalidOperationException("Health component could not be created.");
    }

    private bool ShouldRetain(HealthConditionState condition)
    {
        var definition = GetDefinition(condition.Id);
        if (IsPersistent(definition, condition)) return true;
        return condition.RemainingYears is > 0;
    }

    private static bool IsPersistent(HealthConditionDefinition? definition, HealthConditionState condition)
    {
        if (definition is not null)
        {
            return definition.Course.Equals("Chronic", StringComparison.OrdinalIgnoreCase)
                || definition.Course.Equals("Terminal", StringComparison.OrdinalIgnoreCase)
                || definition.Category.Equals("Birth", StringComparison.OrdinalIgnoreCase)
                || definition.Category.Equals("Childhood", StringComparison.OrdinalIgnoreCase);
        }

        // Unknown legacy conditions retain their serialized behavior.
        return condition.Type.Equals("permanent", StringComparison.OrdinalIgnoreCase)
               || condition.Type.Equals("terminal", StringComparison.OrdinalIgnoreCase)
               || condition.Type.Equals("birth_defect", StringComparison.OrdinalIgnoreCase)
               || condition.Type.Equals("childhood", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateDefinitions(IReadOnlyList<HealthConditionDefinition> definitions)
    {
        if (definitions.Count == 0) throw new InvalidDataException("Health condition data is empty.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in definitions)
        {
            if (string.IsNullOrWhiteSpace(d.Id)
                || string.IsNullOrWhiteSpace(d.Name)
                || string.IsNullOrWhiteSpace(d.Type)
                || string.IsNullOrWhiteSpace(d.Category)
                || string.IsNullOrWhiteSpace(d.Course))
            {
                throw new InvalidDataException(
                    "Every health condition needs id, name, type, category and course.");
            }
            if (!ids.Add(d.Id)) throw new InvalidDataException($"Duplicate health condition ID '{d.Id}'.");
            if (d.DurationMin.HasValue != d.DurationMax.HasValue) throw new InvalidDataException($"Condition '{d.Id}' must specify both durationMin and durationMax, or neither.");
            if (d.DurationMin.HasValue && (d.DurationMin <= 0 || d.DurationMax < d.DurationMin)) throw new InvalidDataException($"Condition '{d.Id}' has an invalid duration.");
            if ((d.Category.Equals("Mild", StringComparison.OrdinalIgnoreCase)
                 || d.Category.Equals("Serious", StringComparison.OrdinalIgnoreCase))
                && d.Weight <= 0)
            {
                throw new InvalidDataException(
                    $"Condition '{d.Id}' needs a positive selection weight.");
            }
        }
    }
}
