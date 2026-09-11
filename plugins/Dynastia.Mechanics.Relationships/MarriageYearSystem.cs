using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class MarriageYearSystem : IYearSystem
{
    private const string MaleNamesPath =
        "Names/polish_male.csv";

    private const string FemaleNamesPath =
        "Names/polish_female.csv";

    private const string SurnamesPath =
        "Names/polish_surnames.csv";

    private static readonly double[] AppealMarriageChance =
        [0, 0.05, 0.07, 0.10, 0.12, 0.16];

    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly ICareerService _career;
    private readonly IGameDataService _data;
    private readonly IGameRandom _random;
    private readonly IGameCalendar _calendar;
    private readonly IGameEventBus _events;

    public MarriageYearSystem(
        IFamilyService family,
        IStatsService stats,
        ICareerService career,
        IGameDataService data,
        IGameRandom random,
        IGameCalendar calendar,
        IGameEventBus events)
    {
        _family = family;
        _stats = stats;
        _career = career;
        _data = data;
        _random = random;
        _calendar = calendar;
        _events = events;
    }

    public string Id =>
        "relationships.marriage";

    public YearPhase Phase =>
        YearPhase.LifeEvents;

    public IReadOnlyCollection<string> Before =>
        ["relationships.affairs"];

    public IReadOnlyCollection<string> After =>
        [
            "actions.queued.life_events",
            "career.employment"
        ];

    public void Execute(IGameState gameState)
    {
        // Match the source: newly generated spouses are not
        // independently processed during the same life-event pass.
        var livingSnapshot =
            gameState.People
                .Where(person =>
                    !person.Tags.Has("state.dead"))
                .ToList();

        foreach (var person in livingSnapshot)
        {
            if (!CanSearch(person))
                continue;

            var appeal =
                GetStat(
                    person,
                    "appeal");

            var marriageChance =
                AppealMarriageChance[
                    Math.Clamp(appeal, 1, 5)];

            if (person.Tags.Has(
                "modifier.find_spouse"))
            {
                marriageChance *= 5;
            }

            person.Tags.Remove(
                "modifier.find_spouse");

            if (_random.NextDouble()
                >= marriageChance)
            {
                continue;
            }

            CreateRelationship(
                gameState,
                person);
        }
    }

    private bool CanSearch(IPerson person)
    {
        return person.Tags.Has("state.alive")
            && _family.GetSex(person) == Sex.Male
            && (
                _family.IsBloodline(person)
                || person.Tags.Has(
                    "simulation.peripheral_ex")
            )
            && _family.GetSpouse(person) is null
            && person.Age >= 18;
    }

    private void CreateRelationship(
        IGameState gameState,
        IPerson person)
    {
        var homosexual =
            person.Tags.Has(
                "sexuality.homosexual");

        var spouseSex =
            homosexual
                ? Sex.Male
                : Sex.Female;

        var spouseName =
            RandomWeightedFrom(
                spouseSex == Sex.Male
                    ? MaleNamesPath
                    : FemaleNamesPath);

        var originalSurname =
            RandomWeightedFrom(
                SurnamesPath);

        var spouseAge =
            RelationshipPersonalityRules.ChoosePartnerAge(
                person,
                spouseSex,
                _random);

        var spouse =
            gameState.CreatePerson(
                spouseName,
                originalSurname,
                spouseAge);

        spouse.BirthDate =
            RandomDateInYear(
                gameState.Year
                - spouse.Age);

        _family.InitializePerson(
            spouse,
            spouseSex);

        spouse.Tags.Add("state.alive");
        spouse.Tags.Add("age.adult");
        spouse.Tags.Add("relationship.single");

        if (person.Tags.Has(
            "simulation.peripheral_ex"))
        {
            spouse.Tags.Add(
                "simulation.peripheral_partner");
        }

        // Person constructor default in the source is heterosexual.
        spouse.Tags.Add(
            "sexuality.heterosexual");

        _stats.EnsureStats(spouse);

        var exceptionalMatch =
            RelationshipPersonalityRules.ApplyExceptionalPartnerStats(
                person,
                spouse,
                _stats,
                _random);

        var spouseEventName =
            _family.GetDisplayName(
                spouse);

        if (!homosexual)
        {
            spouse.MaidenName =
                originalSurname;

            GenerateFamilyBackground(
                spouse,
                originalSurname);

            spouse.Surname =
                person.Surname;
        }

        _family.SetSpouses(
            person,
            spouse,
            gameState.Year);

        var personName =
            _family.GetDisplayName(person);

        var spouseDisplayName =
            spouseEventName;

        var eventType =
            homosexual
                ? "relationship.partnered"
                : "relationship.married";

        var text =
            homosexual
                ? $"{personName} came out as homosexual and " +
                  $"entered a partnership with {spouseDisplayName}."
                : $"{personName} married {spouseDisplayName}.";

        _events.Publish(
            new GameEvent
            {
                Type = eventType,
                Year = gameState.Year,
                SubjectId = person.Id,
                RelatedPersonIds = [spouse.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["spouseId"] =
                            spouse.Id.ToString(),
                        ["text"] = text
                    }
            });

        RelationshipPersonalityRules.ApplyExceptionalPartnerCareer(
            exceptionalMatch,
            spouse,
            _career,
            _random);
    }

    private void GenerateFamilyBackground(
        IPerson spouse,
        string maidenSurname)
    {
        var fatherName =
            $"{RandomWeightedFrom(MaleNamesPath)} " +
            $"{maidenSurname}";

        var motherName =
            $"{RandomWeightedFrom(FemaleNamesPath)} " +
            $"{_family.FormatSurname(maidenSurname, Sex.Female)}";

        var siblings =
            new List<string>();

        var count =
            _random.NextInt(0, 4);

        for (var i = 0; i < count; i++)
        {
            var sex =
                _random.NextDouble() > 0.5
                    ? Sex.Male
                    : Sex.Female;

            var name =
                RandomWeightedFrom(
                    sex == Sex.Male
                        ? MaleNamesPath
                        : FemaleNamesPath);

            var surname =
                _family.FormatSurname(
                    maidenSurname,
                    sex);

            siblings.Add(
                $"{name} {surname}");
        }

        _family.SetGeneratedFamilyBackground(
            spouse,
            new GeneratedFamilyBackgroundInfo(
                fatherName,
                motherName,
                siblings));
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

    private string RandomWeightedFrom(
        string relativePath)
    {
        var entries =
            _data.GetWeightedStringList(
                relativePath);

        var totalWeight =
            entries.Sum(
                entry => (double)entry.Weight);

        var roll =
            _random.NextDouble()
            * totalWeight;

        foreach (var entry in entries)
        {
            if (roll < entry.Weight)
                return entry.Value;

            roll -= entry.Weight;
        }

        return entries[^1].Value;
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
