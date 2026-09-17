using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class StandardHealthService : IHealthService
{
    private const string ConditionsPath = "Common/health_conditions.json";
    private readonly IGameRandom _random;
    private readonly Dictionary<string, HealthConditionDefinition> _definitions;
    private HistoricalHealthCatalog? _historical;

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
            health.Conditions.Select(c =>
            {
                var definition = GetDefinition(c.Id);
                var impact = definition is null
                    ? c.HealthImpact
                    : HealthSeverityRules.ScaleAnnualImpact(definition);

                return new HealthConditionInfo(
                    c.Id,
                    c.Name,
                    c.Type,
                    impact,
                    c.RemainingYears);
            }).ToList());
    }

    internal void ReconcileAll(IEnumerable<IPerson> people)
    {
        foreach (var person in people)
        {
            EnsureHealth(person);
            var health = GetRequired(person);
            health.Maximum = Math.Max(0, health.Maximum);
            health.Current = Math.Clamp(health.Current, 0, health.Maximum);
            ReconcileStoredConditionImpacts(health);
        }
    }

    public void SetHealth(IPerson person, double value)
    {
        var health = GetMutable(person);
        health.Current = Math.Clamp(value, 0, health.Maximum);
    }

    public void ChangeHealth(IPerson person, double amount) =>
        SetHealth(person, GetRequired(person).Current + amount);

    public void ChangeHealthUnclamped(IPerson person, double amount)
    {
        var health = GetMutable(person);
        health.Current = Math.Max(0, health.Current + amount);
    }
    public bool HasCondition(IPerson person, string conditionId) => GetRequired(person).Conditions.Any(x => x.Id.Equals(conditionId, StringComparison.OrdinalIgnoreCase));
    public bool AddCondition(IPerson person, string conditionId) => TryAddCondition(person, conditionId, null, out _);
    public bool AddCondition(IPerson person, string conditionId, int year) => TryAddCondition(person, conditionId, year, out _);
    public bool RemoveCondition(IPerson person, string conditionId) => GetMutable(person).Conditions.RemoveAll(x => x.Id.Equals(conditionId, StringComparison.OrdinalIgnoreCase)) > 0;

    internal IReadOnlyCollection<string> ConditionIds => _definitions.Keys;
    internal IReadOnlyCollection<HealthConditionDefinition> Definitions => _definitions.Values;

    public void ConfigureHistoricalCatalog(HistoricalHealthCatalog historical) =>
        _historical = historical ?? throw new ArgumentNullException(nameof(historical));

    internal HealthConditionDefinition? GetDefinition(string conditionId) => _definitions.TryGetValue(conditionId, out var d) ? d : null;
    internal bool IsFamilyNewsCondition(string conditionId) => GetDefinition(conditionId)?.Newsworthy == true || GetDefinition(conditionId)?.FamilyNews == true;

    internal double ApplyAnnualConditionEffects(IPerson person)
    {
        var health = GetMutable(person);
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

            if (definition is not null)
            {
                condition.HealthImpact =
                    HealthSeverityRules.ScaleAnnualImpact(definition);
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
        int year,
        Func<HealthConditionDefinition, double>? weightModifier,
        out HealthConditionState? added,
        out HealthConditionDefinition? selected)
    {
        var candidates = _definitions.Values
            .Where(d => d.Category.Equals(category, StringComparison.OrdinalIgnoreCase)
                        && IsAvailable(d, age, year)
                        && d.Weight > 0
                        && !HasCondition(person, d.Id))
            .Select(d => new
            {
                Definition = d,
                Weight = d.Weight
                    * Math.Max(0, weightModifier?.Invoke(d) ?? 1)
            })
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

        return TryAddCondition(person, selected.Id, year, out added);
    }

    public static bool IsAvailable(
        HealthConditionDefinition definition,
        int age,
        int year) =>
        age >= definition.MinimumAge
        && (definition.MaximumAge is null || age <= definition.MaximumAge.Value)
        && year >= definition.StartYear
        && (definition.EndYear is null || year <= definition.EndYear.Value);

    internal void ApplyImmediateImpact(IPerson person, HealthConditionDefinition definition)
    {
        var impact = HealthSeverityRules.ScaleImmediateImpact(definition);
        if (impact != 0)
            ChangeHealth(person, impact);
    }

    private bool TryAddCondition(IPerson person, string conditionId, int? year, out HealthConditionState? added)
    {
        var health = GetMutable(person);
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
            Name = year.HasValue
                ? _historical?.GetDisplayName(definition.Id, definition.Name, year.Value)
                    ?? definition.Name
                : definition.Name,
            Type = definition.Type,
            HealthImpact = HealthSeverityRules.ScaleAnnualImpact(definition),
            RemainingYears = remaining
        };
        health.Conditions.Add(added);
        return true;
    }


    private void ReconcileStoredConditionImpacts(HealthComponent health)
    {
        foreach (var condition in health.Conditions)
        {
            var definition = GetDefinition(condition.Id);
            if (definition is null)
                continue;

            condition.HealthImpact =
                HealthSeverityRules.ScaleAnnualImpact(definition);
        }
    }

    private static HealthComponent GetRequired(IPerson person)
    {
        return person.Components.Get<HealthComponent>()
            ?? throw new InvalidOperationException(
                "Health state is missing. Run state reconciliation before reading health data.");
    }

    private HealthComponent GetMutable(IPerson person)
    {
        EnsureHealth(person);
        var health = GetRequired(person);
        health.Maximum = Math.Max(0, health.Maximum);
        health.Current = Math.Clamp(health.Current, 0, health.Maximum);
        return health;
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
            if (d.MinimumAge < 0) throw new InvalidDataException($"Condition '{d.Id}' has a negative minimumAge.");
            if (d.MaximumAge is int maximumAge && maximumAge < d.MinimumAge) throw new InvalidDataException($"Condition '{d.Id}' has maximumAge below minimumAge.");
            if (d.StartYear < GameCalendarConfiguration.GameStartYear) throw new InvalidDataException($"Condition '{d.Id}' starts before {GameCalendarConfiguration.GameStartYear}.");
            if (d.EndYear is int endYear && endYear < d.StartYear) throw new InvalidDataException($"Condition '{d.Id}' has endYear before startYear.");
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
