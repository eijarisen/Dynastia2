using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class StandardHealthService : IHealthService
{
    private const string ConditionsPath =
        "Common/health_conditions.json";

    private readonly IGameRandom _random;

    private readonly Dictionary<string, HealthConditionDefinition>
        _definitions;

    private readonly IReadOnlyList<HealthConditionDefinition>
        _randomIllnesses;

    public StandardHealthService(
        IGameDataService data,
        IGameRandom random)
    {
        _random = random;

        var definitions =
            JsonSerializer.Deserialize<List<HealthConditionDefinition>>(
                data.ReadText(ConditionsPath),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? throw new InvalidDataException(
                $"Could not read {ConditionsPath}.");

        ValidateDefinitions(definitions);

        _definitions =
            definitions.ToDictionary(
                x => x.Id,
                StringComparer.OrdinalIgnoreCase);

        _randomIllnesses =
            definitions
                .Where(x => x.RandomIllness)
                .ToList();
    }

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

        var conditions =
            health.Conditions
                .Select(condition => new HealthConditionInfo(
                    condition.Id,
                    condition.Name,
                    condition.Type,
                    condition.HealthImpact,
                    condition.RemainingYears))
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

        // Source behavior: cap at max health,
        // but do not impose a lower clamp.
        health.Current = Math.Min(
            value,
            health.Maximum);
    }

    public void ChangeHealth(
        IPerson person,
        double amount)
    {
        var health = GetRequired(person);

        SetHealth(
            person,
            health.Current + amount);
    }

    public void ChangeHealthUnclamped(
        IPerson person,
        double amount)
    {
        var health = GetRequired(person);

        health.Current += amount;
    }


    public bool HasCondition(
        IPerson person,
        string conditionId)
    {
        var health = GetRequired(person);

        return health.Conditions.Any(
            x => x.Id.Equals(
                conditionId,
                StringComparison.OrdinalIgnoreCase));
    }

    public bool AddCondition(
        IPerson person,
        string conditionId)
    {
        return TryAddCondition(
            person,
            conditionId,
            out _);
    }

    public bool RemoveCondition(
        IPerson person,
        string conditionId)
    {
        var health = GetRequired(person);

        return health.Conditions.RemoveAll(
            x => x.Id.Equals(
                conditionId,
                StringComparison.OrdinalIgnoreCase)) > 0;
    }

    internal double ApplyAnnualConditionEffects(
        IPerson person)
    {
        var health = GetRequired(person);
        var healthChange = 0.0;

        foreach (var condition in health.Conditions)
        {
            healthChange += condition.HealthImpact;

            if (condition.RemainingYears.HasValue
                && !condition.Id.Equals(
                    "autism",
                    StringComparison.OrdinalIgnoreCase))
            {
                condition.RemainingYears--;
            }
        }

        health.Conditions.RemoveAll(
            condition => !ShouldRetain(condition));

        return healthChange;
    }

    internal bool TryAddRandomIllness(
        IPerson person,
        out HealthConditionState? addedCondition)
    {
        var definition =
            GetWeightedRandomIllness();

        return TryAddCondition(
            person,
            definition.Id,
            out addedCondition);
    }

    private bool TryAddCondition(
        IPerson person,
        string conditionId,
        out HealthConditionState? addedCondition)
    {
        var health = GetRequired(person);

        if (health.Conditions.Any(
            x => x.Id.Equals(
                conditionId,
                StringComparison.OrdinalIgnoreCase)))
        {
            addedCondition = null;
            return false;
        }

        if (!_definitions.TryGetValue(
            conditionId,
            out var definition))
        {
            throw new KeyNotFoundException(
                $"Unknown health condition '{conditionId}'.");
        }

        int? remainingYears = null;

        if (definition.DurationMin.HasValue
            && definition.DurationMax.HasValue)
        {
            remainingYears =
                _random.NextInt(
                    definition.DurationMin.Value,
                    definition.DurationMax.Value);
        }

        addedCondition =
            new HealthConditionState
            {
                Id = definition.Id,
                Name = definition.Name,
                Type = definition.Type,
                HealthImpact = definition.HealthImpact,
                RemainingYears = remainingYears
            };

        health.Conditions.Add(addedCondition);

        return true;
    }

    private HealthConditionDefinition GetWeightedRandomIllness()
    {
        var totalWeight =
            _randomIllnesses.Sum(x => x.Weight);

        var roll =
            _random.NextDouble() * totalWeight;

        foreach (var definition in _randomIllnesses)
        {
            if (roll < definition.Weight)
                return definition;

            roll -= definition.Weight;
        }

        return _randomIllnesses[^1];
    }

    private HealthComponent GetRequired(IPerson person)
    {
        EnsureHealth(person);

        return person.Components.Get<HealthComponent>()
            ?? throw new InvalidOperationException(
                "Health component could not be created.");
    }

    private static bool ShouldRetain(
        HealthConditionState condition)
    {
        if (condition.Type.Equals(
            "permanent",
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (condition.Type.Equals(
            "terminal",
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (condition.Id.Equals(
            "autism",
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return condition.RemainingYears is > 0;
    }

    private static void ValidateDefinitions(
        IReadOnlyList<HealthConditionDefinition> definitions)
    {
        if (definitions.Count == 0)
        {
            throw new InvalidDataException(
                "Health condition data is empty.");
        }

        var ids =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Id)
                || string.IsNullOrWhiteSpace(definition.Name)
                || string.IsNullOrWhiteSpace(definition.Type))
            {
                throw new InvalidDataException(
                    "Every health condition needs id, name and type.");
            }

            if (!ids.Add(definition.Id))
            {
                throw new InvalidDataException(
                    $"Duplicate health condition ID '{definition.Id}'.");
            }

            if (definition.DurationMin.HasValue
                != definition.DurationMax.HasValue)
            {
                throw new InvalidDataException(
                    $"Condition '{definition.Id}' must specify both " +
                    "durationMin and durationMax, or neither.");
            }

            if (definition.DurationMin.HasValue
                && (definition.DurationMin <= 0
                    || definition.DurationMax < definition.DurationMin))
            {
                throw new InvalidDataException(
                    $"Condition '{definition.Id}' has an invalid duration.");
            }

            if (definition.RandomIllness
                && definition.Weight <= 0)
            {
                throw new InvalidDataException(
                    $"Random condition '{definition.Id}' needs a positive weight.");
            }
        }
    }
}
