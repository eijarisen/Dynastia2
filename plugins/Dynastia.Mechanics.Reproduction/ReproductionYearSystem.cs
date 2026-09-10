using Dynastia.Contracts;

namespace Dynastia.Mechanics.Reproduction;

public sealed class ReproductionYearSystem : IYearSystem
{
    private const string MaleNamesPath =
        "Names/polish_male.csv";

    private const string FemaleNamesPath =
        "Names/polish_female.csv";

    private const int MinimumChildbearingAge =
        18;

    private const int MaximumChildbearingAge =
        45;

    private const int FertilityDeclineStartAge =
        30;

    private const double MinimumAgeFactor =
        0.1;

    private const double TryForBabyMultiplier =
        5.0;

    private const double InfertilityChance =
        0.05;

    private static readonly double[]
        FertilityToChildChance =
        [0, 0.05, 0.07, 0.10, 0.12, 0.16];

    private static readonly string[]
        StatIds =
        [
            "immunity",
            "longevity",
            "fertility",
            "appeal",
            "strength",
            "intellect"
        ];

    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly IHealthService _health;
    private readonly IGameDataService _data;
    private readonly IGameRandom _random;
    private readonly IGameCalendar _calendar;
    private readonly IGameEventBus _events;

    private readonly IReadOnlyList<
        BirthConditionDefinition>
        _birthConditions;

    public ReproductionYearSystem(
        IFamilyService family,
        IStatsService stats,
        IHealthService health,
        IGameDataService data,
        IGameRandom random,
        IGameCalendar calendar,
        IGameEventBus events,
        IReadOnlyList<
            BirthConditionDefinition>
            birthConditions)
    {
        _family = family;
        _stats = stats;
        _health = health;
        _data = data;
        _random = random;
        _calendar = calendar;
        _events = events;

        // Keep file order. Probability is direct, not a cumulative threshold.
        _birthConditions =
            birthConditions.ToList();
    }

    public string Id =>
        "reproduction.births";

    public YearPhase Phase =>
        YearPhase.LifeEvents;

    public IReadOnlyCollection<string> Before =>
        ["relationships.female_remarriage"];

    public IReadOnlyCollection<string> After =>
        [
            "actions.queued.life_events",
            "relationships.marriage",
            "relationships.affairs"
        ];

    public void Execute(
        IGameState gameState)
    {
        var livingSnapshot =
            gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive"))
                .ToList();

        foreach (var father in livingSnapshot)
        {
            if (_family.GetSex(father)
                != Sex.Male)
            {
                continue;
            }

            var mother =
                _family.GetSpouse(
                    father);

            if (!CanAttemptBirth(
                gameState,
                father,
                mother))
            {
                father.Tags.Remove(
                    "modifier.try_for_baby");

                continue;
            }

            var childChance =
                CalculateChildChance(
                    father,
                    mother!);

            if (father.Tags.Has(
                "modifier.try_for_baby"))
            {
                childChance *=
                    TryForBabyMultiplier;
            }

            father.Tags.Remove(
                "modifier.try_for_baby");

            if (_random.NextDouble()
                >= childChance)
            {
                continue;
            }

            CreateChild(
                gameState,
                father,
                mother!);
        }
    }

    private bool CanAttemptBirth(
        IGameState gameState,
        IPerson father,
        IPerson? mother)
    {
        if (mother is null)
            return false;

        if (!mother.Tags.Has(
            "state.alive"))
        {
            return false;
        }

        if (_family.GetSex(mother)
            != Sex.Female)
        {
            return false;
        }

        if (mother.Age
            < MinimumChildbearingAge
            || mother.Age
            > MaximumChildbearingAge)
        {
            return false;
        }

        if (mother.Tags.Has(
            "state.imprisoned"))
        {
            return false;
        }

        var activeMarriage =
            _family
                .GetRelationshipHistory(
                    father)
                .LastOrDefault(
                    relationship =>
                        relationship.SpouseId
                            == mother.Id
                        && relationship.EndYear
                            is null);

        if (activeMarriage is not null
            && activeMarriage.StartYear
                == gameState.Year)
        {
            return false;
        }

        return true;
    }

    private double CalculateChildChance(
        IPerson father,
        IPerson mother)
    {
        var fatherFertility =
            GetStat(
                father,
                "fertility");

        var motherFertility =
            GetStat(
                mother,
                "fertility");

        var baseFertility =
            Math.Min(
                fatherFertility,
                motherFertility);

        var chance =
            FertilityToChildChance[
                Math.Clamp(
                    baseFertility,
                    0,
                    5)];

        if (mother.Age
            > FertilityDeclineStartAge)
        {
            var ageRange =
                MaximumChildbearingAge
                - FertilityDeclineStartAge;

            var yearsIntoDecline =
                mother.Age
                - FertilityDeclineStartAge;

            var ageFactor =
                Math.Max(
                    MinimumAgeFactor,
                    1.0
                    - (yearsIntoDecline
                       / (double)ageRange));

            chance *=
                ageFactor;
        }

        return chance;
    }

    private void CreateChild(
        IGameState gameState,
        IPerson father,
        IPerson mother)
    {
        var sex =
            _random.NextDouble() > 0.5
                ? Sex.Female
                : Sex.Male;

        var childName =
            GenerateUniqueChildName(
                father,
                sex);

        var child =
            gameState.CreatePerson(
                childName,
                father.Surname,
                age: 0);

        if (sex == Sex.Female)
        {
            child.MaidenName =
                father.Surname;
        }

        child.BirthDate =
            RandomDateInYear(
                gameState.Year);

        var fatherGeneration =
            _family.GetGeneration(
                father)
            ?? 0;

        _family.InitializePerson(
            child,
            sex,
            generation:
                fatherGeneration + 1);

        child.Tags.Add(
            "state.alive");

        child.Tags.Add(
            "age.child");

        child.Tags.Add(
            "sexuality.heterosexual");

        if (_family.IsBloodline(father)
            || _family.IsBloodline(mother))
        {
            child.Tags.Add(
                "family.bloodline");
        }

        if (sex == Sex.Male
            && _family.IsMaleLineage(
                father))
        {
            child.Tags.Add(
                "lineage.male");
        }

        _family.SetParents(
            child,
            father,
            mother);

        var inheritedStats =
            InheritStats(
                father,
                mother);

        var birthCondition =
            ApplyBirthConditionStatModifiers(
                inheritedStats);

        _stats.SetStats(
            child,
            inheritedStats);

        _health.EnsureHealth(
            child);

        if (birthCondition is not null)
        {
            _health.AddCondition(
                child,
                birthCondition.HealthConditionId);

            PublishBirthConditionEvent(
                gameState,
                child,
                father,
                mother,
                birthCondition);
        }

        PublishBirthEvent(
            gameState,
            child,
            father,
            mother);
    }

    private Dictionary<string, int>
        InheritStats(
            IPerson father,
            IPerson mother)
    {
        var result =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var statId in StatIds)
        {
            var inherited =
                _random.NextDouble() > 0.5
                    ? GetStat(
                        father,
                        statId)
                    : GetStat(
                        mother,
                        statId);

            inherited +=
                _random.NextInt(
                    -1,
                    1);

            result[statId] =
                Math.Clamp(
                    inherited,
                    1,
                    5);
        }

        if (_random.NextDouble()
            < InfertilityChance)
        {
            result["fertility"] =
                0;
        }

        return result;
    }

    private BirthConditionDefinition?
        ApplyBirthConditionStatModifiers(
            Dictionary<string, int> stats)
    {
        // One shared roll keeps birth defects mutually exclusive.
        // Each data entry now supplies its OWN probability rather
        // than a precomputed cumulative threshold.
        var roll =
            _random.NextDouble();

        var cumulative =
            0.0;

        BirthConditionDefinition? condition =
            null;

        foreach (var definition in
            _birthConditions)
        {
            cumulative +=
                definition.Probability;

            if (roll < cumulative)
            {
                condition =
                    definition;

                break;
            }
        }

        if (condition is null)
            return null;

        foreach (var modifier in
            condition.StatModifiers)
        {
            if (!stats.TryGetValue(
                modifier.Key,
                out var current))
            {
                continue;
            }

            stats[modifier.Key] =
                Math.Clamp(
                    current
                    + modifier.Value,
                    1,
                    5);
        }

        return condition;
    }

    private string GenerateUniqueChildName(
        IPerson father,
        Sex sex)
    {
        var usedNames =
            _family
                .GetChildren(
                    father)
                .Select(
                    child =>
                        child.Name)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var path =
            sex == Sex.Male
                ? MaleNamesPath
                : FemaleNamesPath;

        var candidates =
            _data
                .GetWeightedStringList(
                    path)
                .Where(
                    entry =>
                        !usedNames.Contains(
                            entry.Value))
                .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                "No unused first names remain " +
                "for this father's children.");
        }

        return RandomWeightedFrom(
            candidates);
    }

    private string RandomWeightedFrom(
        IReadOnlyList<
            WeightedStringEntry> entries)
    {
        var totalWeight =
            entries.Sum(
                entry =>
                    (double)entry.Weight);

        var roll =
            _random.NextDouble()
            * totalWeight;

        foreach (var entry in entries)
        {
            if (roll
                < entry.Weight)
            {
                return entry.Value;
            }

            roll -=
                entry.Weight;
        }

        return entries[^1].Value;
    }

    private GameDate RandomDateInYear(
        int year)
    {
        var month =
            _random.NextInt(
                1,
                12);

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
        string statId)
    {
        return _stats
            .GetStats(person)
            .First(
                stat =>
                    stat.Id.Equals(
                        statId,
                        StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    private void PublishBirthConditionEvent(
        IGameState gameState,
        IPerson child,
        IPerson father,
        IPerson mother,
        BirthConditionDefinition condition)
    {
        _events.Publish(
            new GameEvent
            {
                Type =
                    "birth.condition",

                Year =
                    gameState.Year,

                SubjectId =
                    child.Id,

                RelatedPersonIds =
                    [
                        father.Id,
                        mother.Id
                    ],

                Data =
                    new Dictionary<string, string>
                    {
                        ["conditionId"] =
                            condition.HealthConditionId,

                        ["condition"] =
                            condition.Name,

                        ["text"] =
                            $"{_family.GetDisplayName(child)} " +
                            $"was born with {condition.Name}."
                    }
            });
    }

    private void PublishBirthEvent(
        IGameState gameState,
        IPerson child,
        IPerson father,
        IPerson mother)
    {
        var fatherCount =
            _family
                .GetChildren(
                    father)
                .Count;

        var motherCount =
            _family
                .GetChildren(
                    mother)
                .Count;

        var fatherOrder =
            ToOrdinalWord(
                fatherCount);

        _events.Publish(
            new GameEvent
            {
                Type =
                    "life.birth",

                Year =
                    gameState.Year,

                SubjectId =
                    child.Id,

                RelatedPersonIds =
                    [
                        father.Id,
                        mother.Id
                    ],

                Data =
                    new Dictionary<string, string>
                    {
                        ["fatherId"] =
                            father.Id.ToString(),

                        ["motherId"] =
                            mother.Id.ToString(),

                        ["fatherCount"] =
                            fatherCount.ToString(),

                        ["motherCount"] =
                            motherCount.ToString(),

                        ["text"] =
                            $"{_family.GetDisplayName(child)} " +
                            $"was born to " +
                            $"{_family.GetDisplayName(father)} and " +
                            $"{_family.GetDisplayName(mother)}. " +
                            $"This is their {fatherOrder} child."
                    }
            });
    }

    private static string ToOrdinalWord(
        int value)
    {
        return value switch
        {
            1 => "first",
            2 => "second",
            3 => "third",
            _ => $"{value}th"
        };
    }
}
