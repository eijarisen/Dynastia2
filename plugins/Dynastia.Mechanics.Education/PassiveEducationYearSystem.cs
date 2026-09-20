using Dynastia.Contracts;

namespace Dynastia.Mechanics.Education;

public sealed class PassiveEducationYearSystem : IYearSystem
{
    private const int StudentAge = 6;
    private const int AdultAge = 18;
    private const double IntellectDivisor = 25.0;
    private const double HealthDivisor = 600.0;

    private readonly IEducationService _education;
    private readonly IStatsService _stats;
    private readonly IHealthService _health;
    private readonly IGameRandom _random;
    private readonly EducationEraCatalog _eras;

    public PassiveEducationYearSystem(
        IEducationService education,
        IStatsService stats,
        IHealthService health,
        IGameRandom random,
        EducationEraCatalog eras)
    {
        _education = education;
        _stats = stats;
        _health = health;
        _random = random;
        _eras = eras;
    }

    public string Id => "education.passive";
    public YearPhase Phase => YearPhase.Status;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => ["aging.increment_age"];

    public void Execute(IGameState gameState)
    {
        var era = _eras.GetRule(gameState.Year);

        foreach (var person in gameState.People)
        {
            if (person.Tags.Has("state.dead")
                || person.Age < StudentAge
                || person.Age >= AdultAge)
            {
                continue;
            }

            var current = _education.GetEducationLevel(person);

            var intellect = _stats.GetStats(person)
                .First(stat =>
                    stat.Id.Equals(
                        "intellect",
                        StringComparison.OrdinalIgnoreCase))
                .Value;

            var localCeiling = _education.GetLocalEducationCeiling(
                person,
                gameState.Year);
            var passiveCeiling =
                EducationProgressionRules.GetLocalPassiveChildhoodCeiling(
                    intellect,
                    era.PassiveMaxLevel,
                    localCeiling);

            if (current >= passiveCeiling)
                continue;

            var health = _health.GetHealth(person).Current;

            var chance =
                intellect / IntellectDivisor
                + health / HealthDivisor;

            chance = PersonalityInfluence.AdjustProbability(
                chance,
                person,
                melancholic: 0.10,
                choleric: -0.10);

            chance =
                EducationProgressionRules.ApplyPassiveChanceMultiplier(
                    chance,
                    era);

            chance = Math.Clamp(
                chance * HouseholdLifestyleRules.GetEducationChanceMultiplier(person),
                0,
                1);

            if (_random.NextDouble() < chance)
                _education.IncreaseEducation(person);
        }
    }
}
