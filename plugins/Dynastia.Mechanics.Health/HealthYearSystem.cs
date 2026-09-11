using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class HealthYearSystem : IYearSystem
{
    private const double BaseIllnessChance = 0.15;
    private const double IllnessHealthFactorDivisor = 150.0;

    private readonly StandardHealthService _health;
    private readonly IStatsService _stats;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly IAnnualHealthModifierRegistry _modifiers;

    public HealthYearSystem(
        StandardHealthService health,
        IStatsService stats,
        IGameRandom random,
        IGameEventBus events,
        IAnnualHealthModifierRegistry modifiers)
    {
        _health = health;
        _stats = stats;
        _random = random;
        _events = events;
        _modifiers = modifiers;
    }

    public string Id => "health.annual";

    public YearPhase Phase => YearPhase.Health;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (person.Tags.Has("state.dead"))
                continue;

            _health.EnsureHealth(person);

            var longevity =
                GetStat(person, "longevity");

            var immunity =
                GetStat(person, "immunity");

            var healthChange =
                longevity * 0.5;

            // Household pressure, retired-wife support, divorced-parent
            // penalties and other registered annual effects all remain
            // part of the normal Health pipeline.
            healthChange +=
                _modifiers.GetAnnualHealthChange(
                    person);

            healthChange +=
                _health.ApplyAnnualConditionEffects(
                    person);

            _health.ChangeHealth(
                person,
                healthChange);

            var currentHealth =
                _health.GetHealth(
                    person)
                .Current;

            var illnessChance =
                (BaseIllnessChance / immunity)
                * (
                    1
                    - currentHealth
                    / IllnessHealthFactorDivisor
                );

            if (_random.NextDouble()
                >= illnessChance)
            {
                continue;
            }

            if (!_health.TryAddRandomIllness(
                    person,
                    out var condition)
                || condition is null)
            {
                continue;
            }

            var familyNews =
                _health.IsFamilyNewsCondition(
                    condition.Id);

            var serious =
                familyNews
                || condition.Type.Equals(
                    "terminal",
                    StringComparison.OrdinalIgnoreCase)
                || condition.Type.Equals(
                    "permanent",
                    StringComparison.OrdinalIgnoreCase);

            _events.Publish(
                new GameEvent
                {
                    Type =
                        serious
                            ? "health.serious_illness"
                            : "health.illness",

                    Year =
                        gameState.Year,

                    SubjectId =
                        person.Id,

                    Data =
                        new Dictionary<string, string>
                        {
                            ["conditionId"] =
                                condition.Id,

                            ["condition"] =
                                condition.Name,

                            ["conditionType"] =
                                condition.Type,

                            ["familyNews"] =
                                familyNews
                                    .ToString()
                                    .ToLowerInvariant(),

                            ["text"] =
                                $"{person.Name} {person.Surname} " +
                                $"fell ill with {condition.Name}."
                        }
                });
        }
    }

    private int GetStat(
        IPerson person,
        string id)
    {
        return _stats
            .GetStats(
                person)
            .First(
                stat =>
                    stat.Id.Equals(
                        id,
                        StringComparison.OrdinalIgnoreCase))
            .Value;
    }
}
