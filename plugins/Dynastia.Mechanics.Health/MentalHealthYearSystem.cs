using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

internal sealed class MentalHealthYearSystem : IYearSystem
{
    private readonly StandardHealthService _health;
    private readonly IFamilyService _family;
    private readonly IExistingLocationService _locations;
    private readonly IStressService _stress;
    private readonly IContextWeightCatalog _contextWeights;
    private readonly StressOutcomeCatalog _outcomes;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public MentalHealthYearSystem(
        StandardHealthService health,
        IFamilyService family,
        IExistingLocationService locations,
        IStressService stress,
        IContextWeightCatalog contextWeights,
        StressOutcomeCatalog outcomes,
        IGameRandom random,
        IGameEventBus events)
    {
        _health = health;
        _family = family;
        _locations = locations;
        _stress = stress;
        _contextWeights = contextWeights;
        _outcomes = outcomes;
        _random = random;
        _events = events;
    }

    public string Id => "health.life_stress";
    public YearPhase Phase => YearPhase.PostYear;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People.Where(person =>
                     person.Tags.Has("state.alive") && !SimulationState.IsInactive(person)))
        {
            var snapshot = _stress.GetStress(person);
            if (snapshot.Total <= 0 || person.Age < 5)
                continue;

            var context = HealthContextProfile.Build(person, gameState.Year, _family, _locations);
            var candidates = _outcomes.Definitions
                .Where(outcome => outcome.IsEligible(gameState.Year, person.Age, snapshot.Total))
                .Where(outcome => !_health.HasCondition(person, outcome.ConditionId))
                .Select(outcome => new
                {
                    Outcome = outcome,
                    Weight = outcome.BaseWeight
                             * _contextWeights.GetMultiplier(outcome.ConditionId, context)
                })
                .Where(candidate => candidate.Weight > 0)
                .ToList();

            if (candidates.Count == 0)
                continue;

            var existingStressConditions = _outcomes.Definitions.Count(outcome =>
                _health.HasCondition(person, outcome.ConditionId));

            var chance = MentalHealthStressRules.GetReactionChance(
                snapshot.Total,
                person,
                existingStressConditions);

            if (_random.NextDouble() >= chance)
                continue;

            var totalWeight = candidates.Sum(candidate => candidate.Weight);
            var roll = _random.NextDouble() * totalWeight;
            var selected = candidates[^1].Outcome;
            foreach (var candidate in candidates)
            {
                if (roll < candidate.Weight)
                {
                    selected = candidate.Outcome;
                    break;
                }
                roll -= candidate.Weight;
            }

            if (!_health.AddCondition(person, selected.ConditionId, gameState.Year))
                continue;

            var conditionName = _health.GetHealth(person).Conditions
                .First(entry => entry.Id.Equals(selected.ConditionId, StringComparison.OrdinalIgnoreCase))
                .Name;

            _events.Publish(new GameEvent
            {
                Type = "health.illness",
                Year = gameState.Year,
                SubjectId = person.Id,
                Data = new Dictionary<string, string>
                {
                    ["conditionId"] = selected.ConditionId,
                    ["condition"] = conditionName,
                    ["familyNews"] = "false",
                    ["lifeStress"] = snapshot.Total.ToString("0.##", CultureInfo.InvariantCulture),
                    ["stressSources"] = string.Join(';', snapshot.Contributions.Select(item => $"{item.SourceId}:{item.Value:0.##}")),
                    ["text"] = $"{_family.GetDisplayName(person)} developed {conditionName} after a difficult period."
                }
            });
        }
    }
}
