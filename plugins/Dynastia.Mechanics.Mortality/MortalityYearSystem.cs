using Dynastia.Contracts;

namespace Dynastia.Mechanics.Mortality;

public sealed class MortalityYearSystem : IYearSystem
{
    private const double AccidentChance = 0.002;

    private const int AgeDeathStartAge = 30;
    private const double AgeDeathFactorThreshold = 0.6;

    private const double BaseLifespan = 40;
    private const double LongevityMultiplier = 12;

    private const double AgeDeathPower = 6;
    private const double AgeDeathImmunityBase = 6;
    private const double AgeDeathImmunityDivisor = 5;
    private const double AgeDeathChanceMultiplier = 0.25;

    private const double TerminalConditionDeathChance = 0.10;
    private const double GriefHealthPenalty = 15;

    private readonly IStatsService _stats;
    private readonly IHealthService _health;
    private readonly IFamilyService _family;
    private readonly IGameRandom _random;
    private readonly IGameCalendar _calendar;
    private readonly IGameEventBus _events;

    public MortalityYearSystem(
        IStatsService stats,
        IHealthService health,
        IFamilyService family,
        IGameRandom random,
        IGameCalendar calendar,
        IGameEventBus events)
    {
        _stats = stats;
        _health = health;
        _family = family;
        _random = random;
        _calendar = calendar;
        _events = events;
    }

    public string Id => "mortality.natural_death";

    public YearPhase Phase => YearPhase.Death;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        ["health.annual"];

    public void Execute(IGameState gameState)
    {
        // Keep family-array order. Immediate grief damage can therefore
        // affect a relative who has not received their death check yet.
        foreach (var person in gameState.People)
        {
            if (person.Tags.Has("state.dead"))
                continue;

            ProcessPerson(gameState, person);
        }
    }

    private void ProcessPerson(
        IGameState gameState,
        IPerson person)
    {
        var healthBeforeAccident =
            _health.GetHealth(person);

        var terminalCount =
            healthBeforeAccident.Conditions.Count(
                condition => condition.Type.Equals(
                    "terminal",
                    StringComparison.OrdinalIgnoreCase));

        var deathChance =
            terminalCount * TerminalConditionDeathChance;

        var accident =
            _random.NextDouble() < AccidentChance;

        if (accident)
            _health.SetHealth(person, 0);

        var longevity =
            GetStat(person, "longevity");

        var immunity =
            GetStat(person, "immunity");

        if (person.Age > AgeDeathStartAge)
        {
            var baseLifespan =
                BaseLifespan
                + longevity * LongevityMultiplier;

            var ageFactor =
                person.Age / baseLifespan;

            if (ageFactor > AgeDeathFactorThreshold)
            {
                deathChance +=
                    Math.Pow(
                        ageFactor,
                        AgeDeathPower)
                    * ((AgeDeathImmunityBase - immunity)
                        / AgeDeathImmunityDivisor)
                    * AgeDeathChanceMultiplier;
            }
        }

        var currentHealth =
            _health.GetHealth(person).Current;

        // Preserve short-circuit behavior:
        // health <= 0 dies without consuming the final death roll.
        var died =
            currentHealth <= 0
            || _random.NextDouble() < deathChance;

        if (!died)
            return;

        var cause =
            accident
                ? "accident"
                : currentHealth <= 0
                    ? "health"
                    : terminalCount > 0
                        ? "illness"
                        : "natural";

        Kill(
            gameState,
            person,
            cause);
    }

    private void Kill(
        IGameState gameState,
        IPerson person,
        string cause)
    {
        var spouse =
            _family.GetSpouse(person);

        var children =
            _family.GetChildren(person)
                .ToList();

        var father =
            _family.GetFather(person);

        var mother =
            _family.GetMother(person);

        person.Tags.Remove("state.alive");
        person.Tags.Remove("control.playable");
        person.Tags.Add("state.dead");

        person.DeathDate =
            RandomDateInYear(
                gameState.Year);

        var related =
            new List<Guid>();

        if (spouse is not null)
            related.Add(spouse.Id);

        related.AddRange(
            children.Select(child => child.Id));

        if (father is not null)
            related.Add(father.Id);

        if (mother is not null)
            related.Add(mother.Id);

        ApplyGrief(spouse);

        foreach (var child in children)
            ApplyGrief(child);

        ApplyGrief(father);
        ApplyGrief(mother);

        // Dynasty 4 clears the surviving spouse's current link,
        // while retaining the deceased person's own spouse reference.
        if (spouse is not null
            && !spouse.Tags.Has("state.dead"))
        {
            _family.ClearCurrentSpouse(spouse);
        }

        _events.Publish(
            new GameEvent
            {
                Type = "life.death",
                Year = gameState.Year,
                SubjectId = person.Id,
                RelatedPersonIds =
                    related.Distinct().ToList(),

                Data = new Dictionary<string, string>
                {
                    ["cause"] = cause,
                    ["age"] = person.Age.ToString(),
                    ["text"] =
                        $"{person.Name} {person.Surname} " +
                        $"died at age {person.Age}."
                }
            });
    }

    private void ApplyGrief(IPerson? relative)
    {
        if (relative is null
            || relative.Tags.Has("state.dead"))
        {
            return;
        }

        _health.ChangeHealth(
            relative,
            -GriefHealthPenalty);
    }

    private GameDate RandomDateInYear(int year)
    {
        var month =
            _random.NextInt(1, 12);

        var day =
            _random.NextInt(
                1,
                _calendar.GetDaysInMonth(
                    year,
                    month));

        return new GameDate(
            Year: year,
            Month: month,
            Day: day);
    }

    private int GetStat(
        IPerson person,
        string id)
    {
        return _stats
            .GetStats(person)
            .First(stat =>
                stat.Id.Equals(
                    id,
                    StringComparison.OrdinalIgnoreCase))
            .Value;
    }
}
