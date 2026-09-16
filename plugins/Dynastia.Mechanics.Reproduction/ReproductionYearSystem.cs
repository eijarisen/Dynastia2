using Dynastia.Contracts;

namespace Dynastia.Mechanics.Reproduction;

public sealed class ReproductionYearSystem : IYearSystem
{
    private const int MinimumChildbearingAge =
        18;

    private const int MaximumChildbearingAge =
        45;

    private const int FertilityDeclineStartAge =
        30;

    private const double MinimumAgeFactor =
        0.1;

    private const int LateFertilityStartAge =
        40;

    private const double LateFertilityAnnualMultiplier =
        0.5;

    private const double TryForBabyMultiplier =
        5.0;

    private const double InfertilityChance =
        0.05;

    private const double TripletChance =
        0.01;

    private const double TwinChance =
        0.04;

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
    private readonly IAppearanceService _appearance;
    private readonly IMarriageSatisfactionService _marriageSatisfaction;
    private readonly IHistoricalNameService _historicalNames;
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
        IAppearanceService appearance,
        IMarriageSatisfactionService marriageSatisfaction,
        IHistoricalNameService historicalNames,
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
        _appearance = appearance;
        _marriageSatisfaction = marriageSatisfaction;
        _historicalNames = historicalNames;
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

            var activelyTried =
                father.Tags.Has(
                    "modifier.try_for_baby");

            if (activelyTried)
            {
                childChance *=
                    TryForBabyMultiplier;
            }

            father.Tags.Remove(
                "modifier.try_for_baby");

            var conceived =
                _random.NextDouble()
                < childChance;

            if (!conceived)
            {
                var satisfactionChange =
                    ReproductionBalanceRules.GetMarriageSatisfactionChange(
                        activelyTried,
                        conceived: false);

                if (satisfactionChange != 0)
                {
                    _marriageSatisfaction.ChangeSatisfactionExact(
                        father,
                        satisfactionChange);
                }

                continue;
            }

            if (IsPeripheralCouple(
                father,
                mother!))
            {
                PublishPeripheralBirth(
                    gameState,
                    father,
                    mother!);

                continue;
            }

            CreateChildren(
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

        // Pregnancy remains possible through age 45, but after 40
        // the already-declining chance is halved again for every
        // additional year of age.
        if (mother.Age
            > LateFertilityStartAge)
        {
            var yearsAfterForty =
                mother.Age
                - LateFertilityStartAge;

            chance *=
                Math.Pow(
                    LateFertilityAnnualMultiplier,
                    yearsAfterForty);
        }

        return chance;
    }

    private bool IsPeripheralCouple(
        IPerson father,
        IPerson mother)
    {
        if (_family.IsBloodline(
                father)
            || _family.IsBloodline(
                mother))
        {
            return false;
        }

        return father.Tags.Has(
                "simulation.peripheral_ex")
            || father.Tags.Has(
                "simulation.peripheral_partner")
            || mother.Tags.Has(
                "simulation.peripheral_ex")
            || mother.Tags.Has(
                "simulation.peripheral_partner");
    }

    private void PublishPeripheralBirth(
        IGameState gameState,
        IPerson father,
        IPerson mother)
    {
        var sex =
            _random.NextDouble() > 0.5
                ? Sex.Female
                : Sex.Male;

        var childName =
            _historicalNames.GetRandomFirstName(
                sex,
                gameState.Year,
                _random);

        var birthDate =
            RandomDateInYear(
                gameState.Year);

        var childSurname =
            _family.FormatSurname(
                father.Surname,
                sex);

        var relationship =
            sex == Sex.Male
                ? "son"
                : "daughter";

        _events.Publish(
            new GameEvent
            {
                Type =
                    "peripheral.birth",

                Year =
                    gameState.Year,

                SubjectId =
                    father.Id,

                RelatedPersonIds =
                    [mother.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["childName"] =
                            childName,

                        ["childSurname"] =
                            childSurname,

                        ["sex"] =
                            sex.ToString(),

                        ["birthDate"] =
                            birthDate.ToString(),

                        ["text"] =
                            $"{_family.GetDisplayName(mother)} and " +
                            $"{_family.GetDisplayName(father)} welcomed " +
                            $"a {relationship}, {childName} {childSurname}."
                    }
            });
    }

    private void CreateChildren(
        IGameState gameState,
        IPerson father,
        IPerson mother)
    {
        var birthCount =
            ResolveBirthCount(
                mother);

        var birthDate =
            RandomDateInYear(
                gameState.Year);

        var fatherCountBefore =
            _family.GetChildren(father).Count;

        var motherCountBefore =
            _family.GetChildren(mother).Count;

        var sharedCountBefore =
            _family.GetChildren(father)
                .Count(candidate =>
                    _family.GetFather(candidate)?.Id == father.Id
                    && _family.GetMother(candidate)?.Id == mother.Id);

        var children =
            new List<IPerson>(birthCount);

        for (var index = 0; index < birthCount; index++)
        {
            children.Add(
                CreateChildEntity(
                    gameState,
                    father,
                    mother,
                    birthDate));
        }

        for (var index = 0; index < children.Count; index++)
        {
            PublishBirthEvent(
                gameState,
                children[index],
                father,
                mother,
                fatherCountBefore + index + 1,
                motherCountBefore + index + 1,
                sharedCountBefore + index + 1,
                children,
                suppressChronicle:
                    index > 0);
        }
    }

    private int ResolveBirthCount(
        IPerson mother)
    {
        if (GetStat(mother, "fertility") != 5)
            return 1;

        if (_random.NextDouble() < TripletChance)
            return 3;

        if (_random.NextDouble() < TwinChance)
            return 2;

        return 1;
    }

    private IPerson CreateChildEntity(
        IGameState gameState,
        IPerson father,
        IPerson mother,
        GameDate birthDate)
    {
        var sex =
            _random.NextDouble() > 0.5
                ? Sex.Female
                : Sex.Male;

        var childName =
            GenerateUniqueChildName(
                father,
                sex,
                birthDate.Year);

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
            birthDate;

        var bloodlineParent =
            _family.IsBloodline(
                father)
                ? father
                : _family.IsBloodline(
                    mother)
                    ? mother
                    : father;

        var parentGeneration =
            _family.GetGeneration(
                bloodlineParent)
            ?? 0;

        _family.InitializePerson(
            child,
            sex,
            generation:
                parentGeneration + 1);

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

        _appearance.EnsureAppearance(
            child);

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

        return child;
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
                    ? GetBaseStat(
                        father,
                        statId)
                    : GetBaseStat(
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

        var longevity =
            stats.TryGetValue(
                "longevity",
                out var inheritedLongevity)
                ? inheritedLongevity
                : 3;

        var condition =
            BirthConditionRules.SelectCondition(
                _birthConditions,
                roll,
                longevity);

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
        Sex sex,
        int birthYear)
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

        return _historicalNames.GetRandomFirstNameExcluding(
            sex,
            birthYear,
            usedNames,
            _random);
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

    private int GetBaseStat(
        IPerson person,
        string statId)
    {
        return _stats
            .GetBaseStats(
                person)
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
        IPerson mother,
        int fatherCount,
        int motherCount,
        int sharedCount,
        IReadOnlyList<IPerson> birthGroup,
        bool suppressChronicle)
    {
        var sharedOrder =
            ToOrdinalWord(
                sharedCount);

        var text =
            birthGroup.Count == 1
                ? $"{_family.GetDisplayName(child)} was born to " +
                  $"{_family.GetDisplayName(father)} and " +
                  $"{_family.GetDisplayName(mother)}. " +
                  $"This is their {sharedOrder} child."
                : BuildMultipleBirthText(
                    mother,
                    birthGroup);

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

                        ["sharedCount"] =
                            sharedCount.ToString(),

                        ["multipleBirthCount"] =
                            birthGroup.Count.ToString(),

                        ["suppressChronicle"] =
                            suppressChronicle
                                ? "true"
                                : "false",

                        ["text"] =
                            text
                    }
            });
    }

    private string BuildMultipleBirthText(
        IPerson mother,
        IReadOnlyList<IPerson> children)
    {
        var kind =
            children.Count == 3
                ? "triplets"
                : "twins";

        var names =
            string.Join(
                ", ",
                children.Select(
                    child =>
                        _family.GetDisplayName(child)));

        return
            $"{_family.GetDisplayName(mother)} gave birth to {kind}: " +
            $"{names}.";
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
