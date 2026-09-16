using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class MarriageYearSystem : IYearSystem
{
    private const string SurnamesPath =
        "Names/polish_surnames.csv";

    private static readonly double[] AppealMarriageChance =
        [0, 0.05, 0.07, 0.10, 0.12, 0.16];

    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly ICareerService _career;
    private readonly IGameDataService _data;
    private readonly IHistoricalNameService _historicalNames;
    private readonly IGameRandom _random;
    private readonly IGameCalendar _calendar;
    private readonly IGameEventBus _events;
    private readonly IRelationshipEraService _relationshipEras;
    private readonly RelationshipEventVariantCatalog _eventVariants;

    public MarriageYearSystem(
        IFamilyService family,
        IStatsService stats,
        ICareerService career,
        IGameDataService data,
        IHistoricalNameService historicalNames,
        IGameRandom random,
        IGameCalendar calendar,
        IGameEventBus events,
        IRelationshipEraService relationshipEras,
        RelationshipEventVariantCatalog eventVariants)
    {
        _family = family;
        _stats = stats;
        _career = career;
        _data = data;
        _historicalNames = historicalNames;
        _random = random;
        _calendar = calendar;
        _events = events;
        _relationshipEras = relationshipEras;
        _eventVariants = eventVariants;
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
            var legacyRequested =
                person.Tags.Has("modifier.find_spouse");

            // Playable/autonomous dynasty households now choose from the
            // concrete candidate pool through the Find a Spouse action.
            // Keep the old automatic path only for peripheral simulation
            // and for queued actions restored from an older save.
            if (!legacyRequested
                && !person.Tags.Has("simulation.peripheral_ex"))
            {
                continue;
            }

            if (!CanSearch(person))
            {
                person.Tags.Remove("modifier.find_spouse");
                continue;
            }

            var appeal =
                GetStat(
                    person,
                    "appeal");

            var marriageChance =
                _relationshipEras
                    .GetRule(gameState.Year)
                    .ApplyMarriageChance(
                        AppealMarriageChance[
                            Math.Clamp(appeal, 1, 5)]);

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
        var partnerSex =
            person.Tags.Has(
                "sexuality.homosexual")
                ? Sex.Male
                : Sex.Female;

        return person.Tags.Has("state.alive")
            && !SimulationState.IsInactive(person)
            && _family.GetSex(person) == Sex.Male
            && (
                _family.IsBloodline(person)
                || person.Tags.Has(
                    "simulation.peripheral_ex")
            )
            && _family.GetSpouse(person) is null
            && person.Age >= 18
            && RelationshipPersonalityRules.CanFindPartner(
                person,
                partnerSex);
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

        var spouseNameSample =
            _random.NextDouble();

        var originalSurname =
            RandomWeightedFrom(
                SurnamesPath);

        if (!RelationshipPersonalityRules.TryChoosePartnerAge(
                person,
                spouseSex,
                _random,
                out var spouseAge))
        {
            return;
        }

        var spouseBirthYear =
            gameState.Year - spouseAge;

        var spouseName =
            _historicalNames.GetRandomFirstName(
                spouseSex,
                spouseBirthYear,
                new FixedSampleGameRandom(
                    spouseNameSample));

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

        GeneratedFamilyBackgroundGenerator.Assign(
            spouse,
            originalSurname,
            _family,
            _historicalNames,
            _random);

        if (!homosexual)
        {
            spouse.MaidenName =
                originalSurname;

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

        var sameSexVariant =
            homosexual
                ? _eventVariants.GetVariant(
                    "relationship.same_sex_union",
                    gameState.Year)
                : null;

        var eventType =
            sameSexVariant?.EventType
            ?? "relationship.married";

        var text =
            sameSexVariant is null
                ? $"{personName} married {spouseDisplayName}."
                : sameSexVariant.FormatText(
                    personName,
                    spouseDisplayName);

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
                        ["text"] = text,
                        ["biographyVerb"] =
                            sameSexVariant?.FormatBiographyVerb(
                                spouseDisplayName)
                            ?? string.Empty
                    }
            });

        RelationshipPersonalityRules.ApplyExceptionalPartnerCareer(
            exceptionalMatch,
            spouse,
            _career,
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
