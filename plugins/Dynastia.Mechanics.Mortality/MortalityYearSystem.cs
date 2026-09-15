using Dynastia.Contracts;

namespace Dynastia.Mechanics.Mortality;

public sealed class MortalityYearSystem : IYearSystem
{
    private const double TerminalConditionDeathChance = 0.10;

    private readonly IStatsService _stats;
    private readonly IHealthService _health;
    private readonly IGameRandom _random;
    private readonly MortalityDeathService _deaths;

    internal MortalityYearSystem(
        IStatsService stats,
        IHealthService health,
        IGameRandom random,
        MortalityDeathService deaths)
    {
        _stats = stats;
        _health = health;
        _random = random;
        _deaths = deaths;
    }

    public string Id => "mortality.natural_death";
    public YearPhase Phase => YearPhase.Death;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => ["health.annual"];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (person.Tags.Has("state.dead")
                || SimulationState.IsInactive(person))
            {
                continue;
            }

            ProcessPerson(gameState, person);
        }
    }

    private void ProcessPerson(IGameState gameState, IPerson person)
    {
        var healthBeforeAccident = _health.GetHealth(person);

        // Depleted Health is deterministic mortality, not part of the scaled
        // random-death model. Resolve it before consuming any accident or
        // natural-death roll so Longevity 5 Second Wind gets its normal chance.
        if (healthBeforeAccident.Current <= 0)
        {
            _deaths.TryResolveZeroHealth(gameState, person);
            return;
        }

        var terminalCount = healthBeforeAccident.Conditions.Count(
            condition => condition.Type.Equals(
                "terminal",
                StringComparison.OrdinalIgnoreCase));

        var longevity = GetStat(person, "longevity");
        var immunity = GetStat(person, "immunity");

        var illnessDeathChance =
            MortalityRules.ScaleRandomMortality(
                terminalCount
                * TerminalConditionDeathChance);

        var naturalDeathChance =
            MortalityRules.GetNaturalDeathChance(
                person.Age,
                longevity,
                immunity);

        var deathChance =
            Math.Clamp(
                illnessDeathChance
                + naturalDeathChance,
                0,
                1);

        var accidentChance = PersonalityInfluence.AdjustProbability(
            MortalityRules.GenericAccidentChance,
            person,
            melancholic: -0.10,
            phlegmatic: -0.20,
            sanguine: 0.15,
            choleric: 0.20);

        var accident = _random.NextDouble() < accidentChance;
        if (accident)
        {
            _health.SetHealth(person, 0);
            _deaths.Kill(gameState, person, "accident");
            return;
        }

        if (_random.NextDouble() >= deathChance)
            return;

        _deaths.Kill(
            gameState,
            person,
            terminalCount > 0 ? "illness" : "natural");
    }

    private int GetStat(IPerson person, string id) =>
        _stats.GetStats(person)
            .First(stat => stat.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            .Value;
}
