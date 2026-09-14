using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class FemaleRemarriageYearSystem :
    IYearSystem
{
    private const double RemarriageChance =
        0.05;

    private const int MarriageAge =
        18;

    private const int RemarriageMaxAge =
        50;

    private const string SurnamesPath =
        "Names/polish_surnames.csv";

    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly IHealthService _health;
    private readonly IEducationService _education;
    private readonly ICareerService _career;
    private readonly IGameDataService _data;
    private readonly IHistoricalNameService _historicalNames;
    private readonly IGameRandom _random;
    private readonly IGameCalendar _calendar;
    private readonly IGameEventBus _events;

    public FemaleRemarriageYearSystem(
        IFamilyService family,
        IStatsService stats,
        IHealthService health,
        IEducationService education,
        ICareerService career,
        IGameDataService data,
        IHistoricalNameService historicalNames,
        IGameRandom random,
        IGameCalendar calendar,
        IGameEventBus events)
    {
        _family = family;
        _stats = stats;
        _health = health;
        _education = education;
        _career = career;
        _data = data;
        _historicalNames = historicalNames;
        _random = random;
        _calendar = calendar;
        _events = events;
    }

    public string Id =>
        "relationships.female_remarriage";

    public YearPhase Phase =>
        YearPhase.LifeEvents;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        [
            "relationships.affairs",
            "reproduction.births"
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

        foreach (var woman in
            livingSnapshot)
        {
            if (SimulationState.IsInactive(woman)
                || _family.GetSex(woman)
                    != Sex.Female
                || _family.GetSpouse(
                    woman) is not null
                || woman.Age
                    < MarriageAge
                || woman.Age
                    >= RemarriageMaxAge
                || woman.Tags.Has(
                    "control.playable")
                || _family.IsMaleLineage(
                    woman)
                || (
                    !_family.IsBloodline(
                        woman)
                    && !woman.Tags.Has(
                        "simulation.peripheral_ex")
                )
                || HasDivorceAffectedMinorChild(
                    woman))
            {
                continue;
            }

            if (_random.NextDouble()
                >= RemarriageChance)
            {
                continue;
            }

            CreateHusband(
                gameState,
                woman);
        }
    }

    private bool HasDivorceAffectedMinorChild(
        IPerson woman)
    {
        return _family.GetChildren(
                woman)
            .Any(
                child =>
                    child.Tags.Has(
                        "state.alive")
                    && child.Age < 18
                    && child.Tags.Has(
                        DivorcedParentsTracker.Tag));
    }

    private void CreateHusband(
        IGameState gameState,
        IPerson woman)
    {
        var husbandNameSample =
            _random.NextDouble();

        var husbandSurname =
            RandomWeightedFrom(
                SurnamesPath);

        var husbandAge =
            RelationshipPersonalityRules.ChoosePartnerAge(
                woman,
                Sex.Male,
                _random);

        var husbandBirthYear =
            gameState.Year - husbandAge;

        var husband =
            gameState.CreatePerson(
                _historicalNames.GetRandomFirstName(
                    Sex.Male,
                    husbandBirthYear,
                    new FixedSampleGameRandom(
                        husbandNameSample)),
                husbandSurname,
                husbandAge);

        husband.BirthDate =
            RandomDateInYear(
                gameState.Year
                - husband.Age);

        _family.InitializePerson(
            husband,
            Sex.Male,
            generation: null);

        husband.Tags.Add(
            "state.alive");

        husband.Tags.Add(
            "age.adult");

        husband.Tags.Add(
            "relationship.single");

        if (woman.Tags.Has(
            "simulation.peripheral_ex"))
        {
            husband.Tags.Add(
                "simulation.peripheral_partner");
        }

        husband.Tags.Add(
            "sexuality.heterosexual");

        GeneratedFamilyBackgroundGenerator.Assign(
            husband,
            husband.Surname,
            _family,
            _historicalNames,
            _random);

        // No lineage.male and no family.bloodline:
        // this is an external husband of a female branch.
        _stats.EnsureStats(
            husband);

        var exceptionalMatch =
            RelationshipPersonalityRules.ApplyExceptionalPartnerStats(
                woman,
                husband,
                _stats,
                _random);

        _health.EnsureHealth(
            husband);

        _education.SetEducationLevel(
            husband,
            0);

        var womanEventName =
            _family.GetDisplayName(
                woman);

        var husbandEventName =
            _family.GetDisplayName(
                husband);

        var hadPriorRelationship =
            _family.GetRelationshipHistory(
                woman)
            .Count > 0;

        _family.SetSpouses(
            woman,
            husband,
            gameState.Year);

        woman.MaidenName ??=
            woman.Surname;

        woman.Surname =
            husband.Surname;

        // Pending inheritance follows the bloodline person. The
        // post-inheritance household-claim system transfers it only after
        // this marriage has produced a real household.
        _events.Publish(
            new GameEvent
            {
                Type =
                    hadPriorRelationship
                        ? "relationship.remarried"
                        : "relationship.married",

                Year =
                    gameState.Year,

                SubjectId =
                    woman.Id,

                RelatedPersonIds =
                    [husband.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["text"] =
                            hadPriorRelationship
                                ? $"{womanEventName} " +
                                  $"has remarried to " +
                                  $"{husbandEventName}."
                                : $"{womanEventName} married " +
                                  $"{husbandEventName}."
                    }
            });

        RelationshipPersonalityRules.ApplyExceptionalPartnerCareer(
            exceptionalMatch,
            husband,
            _career,
            _random);
    }

    private string RandomWeightedFrom(
        string relativePath)
    {
        var entries =
            _data.GetWeightedStringList(
                relativePath);

        var totalWeight =
            entries.Sum(
                entry =>
                    (double)entry.Weight);

        var roll =
            _random.NextDouble()
            * totalWeight;

        foreach (var entry in
            entries)
        {
            if (roll < entry.Weight)
                return entry.Value;

            roll -= entry.Weight;
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
            year,
            month,
            day);
    }
}
