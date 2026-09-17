using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class HealthYearSystem : IYearSystem
{
    private const double NaturalRecoveryChance = 0.03;
    private readonly StandardHealthService _health;
    private readonly IStatsService _stats;
    private readonly IFamilyService _family;
    private readonly IExistingLocationService _locations;
    private readonly IContextWeightCatalog _contextWeights;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly IAnnualHealthModifierRegistry _modifiers;

    public HealthYearSystem(
        StandardHealthService health,
        IStatsService stats,
        IFamilyService family,
        IExistingLocationService locations,
        IContextWeightCatalog contextWeights,
        IGameRandom random,
        IGameEventBus events,
        IAnnualHealthModifierRegistry modifiers)
    {
        _health = health;
        _stats = stats;
        _family = family;
        _locations = locations;
        _contextWeights = contextWeights;
        _random = random;
        _events = events;
        _modifiers = modifiers;
    }

    public string Id => "health.annual";
    public YearPhase Phase => YearPhase.Health;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (person.Tags.Has("state.dead") || SimulationState.IsInactive(person)) continue;
            _health.EnsureHealth(person);
            var longevity = GetStat(person, "longevity");
            var immunity = GetStat(person, "immunity");

            var change = longevity * 0.5 + _modifiers.GetAnnualHealthChange(person) + _health.ApplyAnnualConditionEffects(person);
            _health.ChangeHealth(person, change);
            TryNaturalRecovery(gameState, person, immunity);
            TryMildCondition(gameState, person, immunity);
            TrySeriousCondition(gameState, person, longevity);
        }
    }

    private void TryMildCondition(IGameState state, IPerson person, int immunity)
    {
        var chance = immunity switch { 1 => .28, 2 => .20, 3 => .14, 4 => .09, _ => .05 };
        chance = HealthIncidenceRules.ScaleMildConditionChance(chance);
        if (_random.NextDouble() >= chance) return;
        var context = HealthContextProfile.Build(person, state.Year, _family, _locations);
        if (!_health.TryAddWeightedCondition(
                person,
                "Mild",
                person.Age,
                state.Year,
                d =>
                {
                    var weight = _contextWeights.GetMultiplier(d.Id, context);
                    if (d.GeneticTag is not null && person.Tags.Has(d.GeneticTag))
                        weight *= 2.75;
                    return weight;
                },
                out var added,
                out var definition)
            || definition is null
            || added is null)
        {
            return;
        }
        PublishCondition(state, person, definition, added, serious: false);
    }

    private void TrySeriousCondition(IGameState state, IPerson person, int longevity)
    {
        var baseChance = person.Age switch { < 18 => .002, < 40 => .004, < 60 => .010, < 75 => .020, _ => .035 };
        var multiplier = longevity switch { 1 => 1.80, 2 => 1.40, 3 => 1.00, 4 => .70, _ => .45 };
        var chance = HealthIncidenceRules.ScaleSeriousConditionChance(baseChance * multiplier);
        if (_random.NextDouble() >= chance) return;
        var context = HealthContextProfile.Build(person, state.Year, _family, _locations);

        double Weight(HealthConditionDefinition d)
        {
            var weight = 1.0;
            if (d.GeneticTag is not null && person.Tags.Has(d.GeneticTag)) weight *= 2.75;
            if (d.Id.Equals("heart_attack", StringComparison.OrdinalIgnoreCase) && (person.Tags.Has("genetic.heart") || _health.HasCondition(person, "heart_disease"))) weight *= 2.5;
            if (d.Id.Equals("stroke", StringComparison.OrdinalIgnoreCase) && (person.Tags.Has("genetic.heart") || _health.HasCondition(person, "hypertension"))) weight *= 2.5;
            if (d.Id.Equals("chronic_liver_disease", StringComparison.OrdinalIgnoreCase) && _health.HasCondition(person, "alcoholism")) weight *= 4.0;
            weight *= _contextWeights.GetMultiplier(d.Id, context);
            return weight;
        }

        if (!_health.TryAddWeightedCondition(
                person,
                "Serious",
                person.Age,
                state.Year,
                Weight,
                out var added,
                out var definition)
            || definition is null
            || added is null)
        {
            return;
        }
        _health.ApplyImmediateImpact(person, definition);
        PublishCondition(state, person, definition, added, serious: true);
    }

    private void PublishCondition(
        IGameState state,
        IPerson person,
        HealthConditionDefinition definition,
        HealthConditionState condition,
        bool serious)
    {
        var familyNews = serious && definition.Newsworthy;
        _events.Publish(new GameEvent
        {
            Type = familyNews ? "health.serious_illness" : "health.illness",
            Year = state.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["conditionId"] = definition.Id,
                ["condition"] = condition.Name,
                ["conditionType"] = definition.Type,
                ["familyNews"] = familyNews.ToString().ToLowerInvariant(),
                ["text"] = $"{_family.GetDisplayName(person)} fell ill with {condition.Name}."
            }
        });
    }

    private void TryNaturalRecovery(IGameState state, IPerson person, int immunity)
    {
        if (immunity != 5) return;
        var eligible = _health.GetHealth(person).Conditions
            .Select(c => (Condition: c, Definition: _health.GetDefinition(c.Id)))
            .Where(x => x.Definition is not null
                        && x.Definition.Category.Equals("Serious", StringComparison.OrdinalIgnoreCase)
                        && !x.Definition.Course.Equals("Chronic", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var entry in eligible)
        {
            if (_random.NextDouble() >= NaturalRecoveryChance || !_health.RemoveCondition(person, entry.Condition.Id)) continue;
            _events.Publish(new GameEvent
            {
                Type = "health.natural_recovery", Year = state.Year, SubjectId = person.Id,
                Data = new Dictionary<string, string>
                {
                    ["conditionId"] = entry.Condition.Id,
                    ["condition"] = entry.Condition.Name,
                    ["familyNews"] = "true",
                    ["text"] = $"Against expectations, {person.Name} recovered from {entry.Condition.Name}."
                }
            });
        }
    }

    private int GetStat(IPerson person, string id) => _stats.GetStats(person).First(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Value;
}
