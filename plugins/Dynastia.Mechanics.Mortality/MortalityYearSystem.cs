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
    private const double SecondWindChance = 0.25;

    private readonly IStatsService _stats;
    private readonly IHealthService _health;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IGameRandom _random;
    private readonly IGameCalendar _calendar;
    private readonly IGameEventBus _events;

    public MortalityYearSystem(
        IStatsService stats,
        IHealthService health,
        IFamilyService family,
        IEconomyService economy,
        IGameRandom random,
        IGameCalendar calendar,
        IGameEventBus events)
    {
        _stats = stats;
        _health = health;
        _family = family;
        _economy = economy;
        _random = random;
        _calendar = calendar;
        _events = events;
    }

    public string Id =>
        "mortality.natural_death";

    public YearPhase Phase =>
        YearPhase.Death;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        ["health.annual"];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (person.Tags.Has("state.dead")
                || SimulationState.IsInactive(person))
            {
                continue;
            }

            ProcessPerson(
                gameState,
                person);
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
            terminalCount
            * TerminalConditionDeathChance;

        var accidentChance =
            PersonalityInfluence.AdjustProbability(
                AccidentChance,
                person,
                melancholic: -0.10,
                phlegmatic: -0.20,
                sanguine: 0.15,
                choleric: 0.20);

        var accident =
            _random.NextDouble()
            < accidentChance;

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

        if (!accident
            && currentHealth <= 0
            && longevity == 5
            && _random.NextDouble() < SecondWindChance)
        {
            _health.SetHealth(
                person,
                10);

            _events.Publish(
                new GameEvent
                {
                    Type = "health.second_wind",
                    Year = gameState.Year,
                    SubjectId = person.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["familyNews"] = "true",
                        ["text"] =
                            $"Despite being at death's door, " +
                            $"{_family.GetDisplayName(person)} " +
                            "unexpectedly pulled through."
                    }
                });

            return;
        }

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

        var siblings =
            GetSiblings(person);

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
            children.Select(
                child => child.Id));

        if (father is not null)
            related.Add(father.Id);

        if (mother is not null)
            related.Add(mother.Id);

        related.AddRange(
            siblings.Select(sibling => sibling.Id));

        ApplyGrief(
            spouse,
            person,
            extendedRelative: false);

        foreach (var child in children)
        {
            ApplyGrief(
                child,
                person,
                extendedRelative: true);
        }

        ApplyGrief(
            father,
            person,
            extendedRelative: true);

        ApplyGrief(
            mother,
            person,
            extendedRelative: true);

        foreach (var sibling in siblings)
        {
            ApplyGrief(
                sibling,
                person,
                extendedRelative: true);
        }

        if (spouse is not null)
        {
            _family.EndRelationship(
                person,
                spouse,
                gameState.Year,
                "death",
                clearFirst: false,
                clearSecond:
                    !spouse.Tags.Has("state.dead"));
        }

        var survivors = BuildSurvivorText(person, spouse, children);

        _events.Publish(
            new GameEvent
            {
                Type = "life.death",
                Year = gameState.Year,
                SubjectId = person.Id,

                RelatedPersonIds =
                    related
                        .Distinct()
                        .ToList(),

                Data =
                    new Dictionary<string, string>
                    {
                        ["cause"] = cause,
                        ["age"] =
                            person.Age.ToString(),
                        ["text"] =
                            $"{_family.GetDisplayName(person)} " +
                            $"died at age {person.Age}." + survivors
                    }
            });
    }

    private string BuildSurvivorText(
        IPerson person,
        IPerson? spouse,
        IReadOnlyList<IPerson> children)
    {
        var livingSpouse = spouse is not null && spouse.Tags.Has("state.alive")
            ? spouse
            : null;

        var livingChildren = children
            .Where(child => child.Tags.Has("state.alive"))
            .Select(child => child.Name)
            .ToList();

        if (livingSpouse is null && livingChildren.Count == 0)
            return string.Empty;

        var pronoun = _family.GetSex(person) == Sex.Female ? " She" : " He";
        var parts = new List<string>();

        if (livingSpouse is not null)
            parts.Add($"spouse {livingSpouse.Name}");

        if (livingChildren.Count > 0)
        {
            parts.Add(livingChildren.Count == 1
                ? $"child {livingChildren[0]}"
                : $"children {string.Join(", ", livingChildren)}");
        }

        return $"{pronoun} left " + string.Join(" and ", parts) + ".";
    }

    private void ApplyGrief(
        IPerson? relative,
        IPerson deceased,
        bool extendedRelative)
    {
        if (relative is null
            || relative.Tags.Has("state.dead")
            || SimulationState.IsInactive(relative))
        {
            return;
        }

        var sameHousehold =
            AreInSameHousehold(
                relative,
                deceased);

        var penalty =
            FamilyShockRules.ScaleHealthLoss(
                relative,
                GriefHealthPenalty,
                sameHousehold,
                extendedRelative);

        _health.ChangeHealth(
            relative,
            -penalty);
    }

    private IReadOnlyList<IPerson> GetSiblings(
        IPerson person)
    {
        var result =
            new Dictionary<Guid, IPerson>();

        var father = _family.GetFather(person);
        var mother = _family.GetMother(person);

        if (father is not null)
        {
            foreach (var sibling in _family.GetChildren(father))
            {
                if (sibling.Id != person.Id)
                    result[sibling.Id] = sibling;
            }
        }

        if (mother is not null)
        {
            foreach (var sibling in _family.GetChildren(mother))
            {
                if (sibling.Id != person.Id)
                    result[sibling.Id] = sibling;
            }
        }

        return result.Values.ToList();
    }

    private bool AreInSameHousehold(
        IPerson first,
        IPerson second)
    {
        var firstId =
            _economy.GetHouseholdId(first);

        var secondId =
            _economy.GetHouseholdId(second);

        return firstId is not null
            && secondId is not null
            && firstId == secondId;
    }

    private GameDate RandomDateInYear(
        int year)
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
