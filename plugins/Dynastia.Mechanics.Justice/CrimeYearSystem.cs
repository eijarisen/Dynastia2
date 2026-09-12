using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class CrimeYearSystem :
    IYearSystem
{
    private const double BaseCrimeChance =
        0.005;

    private const double ImprisonedDivorceChance =
        0.10;

    private const double CrimeSpouseHealthPenalty =
        20;

    private const double CrimeChildHealthPenalty =
        15;

    private const string SurnamesPath =
        "Names/polish_surnames.csv";

    private readonly StandardJusticeService _justice;
    private readonly IFamilyService _family;
    private readonly IHealthService _health;
    private readonly ICareerService _career;
    private readonly IEconomyService _economy;
    private readonly IGameDataService _data;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    private readonly IReadOnlyList<
        CrimeDefinition> _crimes;

    public CrimeYearSystem(
        StandardJusticeService justice,
        IFamilyService family,
        IHealthService health,
        ICareerService career,
        IEconomyService economy,
        IGameDataService data,
        IGameRandom random,
        IGameEventBus events,
        IReadOnlyList<CrimeDefinition> crimes)
    {
        _justice = justice;
        _family = family;
        _health = health;
        _career = career;
        _economy = economy;
        _data = data;
        _random = random;
        _events = events;
        _crimes = crimes;
    }

    public string Id =>
        "justice.crime_and_prison_divorce";

    public YearPhase Phase =>
        YearPhase.LifeEvents;

    public IReadOnlyCollection<string> Before =>
        ["career.employment"];

    public IReadOnlyCollection<string> After =>
        ["actions.queued.life_events"];

    public void Execute(
        IGameState gameState)
    {
        var living =
            gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive")
                        && !SimulationState.IsInactive(
                            person))
                .ToList();

        foreach (var person in
            living)
        {
            var spouse =
                _family.GetSpouse(
                    person);

            var crimeChance =
                PersonalityInfluence.AdjustProbability(
                    BaseCrimeChance,
                    person,
                    good: -0.20,
                    evil: 0.20);

            if (person.Age >= 18
                && !_justice.IsImprisoned(
                    person)
                && _random.NextDouble()
                    < crimeChance)
            {
                CommitCrime(
                    gameState,
                    person,
                    spouse);

                // Match source if/else-if:
                // no prison-divorce roll in the same year
                // the crime itself is committed.
                continue;
            }

            if (_justice.IsImprisoned(
                    person)
                && spouse is not null
                && _random.NextDouble()
                    < PersonalityInfluence.AdjustProbability(
                        ImprisonedDivorceChance,
                        spouse,
                        phlegmatic: -0.10,
                        good: -0.20))
            {
                DivorceImprisonedSpouse(
                    gameState,
                    person,
                    spouse);
            }
        }
    }

    private void CommitCrime(
        IGameState gameState,
        IPerson person,
        IPerson? spouse)
    {
        var crime =
            GetWeightedCrime();

        var sentence =
            _random.NextInt(
                crime.SentenceMin,
                crime.SentenceMax);

        _career.SetJobLevel(
            person,
            0);

        _justice.Imprison(
            person,
            crime,
            sentence);

        var sentenceText =
            sentence > 50
                ? "life"
                : sentence == 1
                    ? "1 year"
                    : $"{sentence} years";

        _events.Publish(
            new GameEvent
            {
                Type =
                    "justice.crime",

                Year =
                    gameState.Year,

                SubjectId =
                    person.Id,

                RelatedPersonIds =
                    spouse is null
                        ? []
                        : [spouse.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["crimeId"] =
                            crime.Id,

                        ["crime"] =
                            crime.Name,

                        ["sentence"] =
                            sentence.ToString(),

                        ["text"] =
                            $"{_family.GetDisplayName(person)} " +
                            $"was sentenced to {sentenceText} in prison " +
                            $"after {crime.Description}"
                    }
            });

        if (spouse is not null)
        {
            ApplyEmotionalHealthLoss(
                spouse,
                CrimeSpouseHealthPenalty);
        }

        foreach (var child in
            _family.GetChildren(
                person))
        {
            ApplyEmotionalHealthLoss(
                child,
                CrimeChildHealthPenalty);
        }
    }

    private void DivorceImprisonedSpouse(
        IGameState gameState,
        IPerson person,
        IPerson spouse)
    {
        ApplyDivorceHealthShock(
            person,
            spouse);

        _events.Publish(
            new GameEvent
            {
                Type =
                    "relationship.prison_divorce",

                Year =
                    gameState.Year,

                SubjectId =
                    person.Id,

                RelatedPersonIds =
                    [spouse.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["text"] =
                            $"{_family.GetDisplayName(spouse)} " +
                            $"divorced the imprisoned " +
                            $"{_family.GetDisplayName(person)}."
                    }
            });

        // Preserve the source's exact "wife" selection:
        // if the prisoner is Female, she is the wife;
        // otherwise spouse is treated as the wife, even in
        // an unusual same-sex edge case.
        var wife =
            _family.GetSex(person)
                == Sex.Female
                    ? person
                    : spouse;

        wife.Surname =
            !string.IsNullOrWhiteSpace(
                wife.MaidenName)
                ? wife.MaidenName
                : RandomWeightedSurname();

        _family.EndRelationship(
            person,
            spouse,
            gameState.Year,
            "divorce");
    }

    private CrimeDefinition GetWeightedCrime()
    {
        var totalWeight =
            _crimes.Sum(
                crime =>
                    crime.Weight);

        var roll =
            _random.NextDouble()
            * totalWeight;

        foreach (var crime in
            _crimes)
        {
            if (roll < crime.Weight)
                return crime;

            roll -= crime.Weight;
        }

        return _crimes[^1];
    }


    private void ApplyDivorceHealthShock(
        IPerson first,
        IPerson second)
    {
        ApplyFamilySituationLoss(
            first,
            second,
            10,
            extendedRelative: false);

        ApplyFamilySituationLoss(
            second,
            first,
            10,
            extendedRelative: false);

        var affectedExtended =
            new HashSet<Guid>();

        foreach (var subject in new[] { first, second })
        {
            foreach (var child in _family.GetChildren(subject))
            {
                if (child.Id != first.Id
                    && child.Id != second.Id
                    && affectedExtended.Add(child.Id))
                {
                    ApplyFamilySituationLoss(
                        child,
                        subject,
                        10,
                        extendedRelative: true);
                }
            }

            var father = _family.GetFather(subject);
            var mother = _family.GetMother(subject);

            foreach (var relative in new[] { father, mother })
            {
                if (relative is not null
                    && relative.Id != first.Id
                    && relative.Id != second.Id
                    && affectedExtended.Add(relative.Id))
                {
                    ApplyFamilySituationLoss(
                        relative,
                        subject,
                        7,
                        extendedRelative: true);
                }
            }

            foreach (var sibling in GetSiblings(subject))
            {
                if (sibling.Id != first.Id
                    && sibling.Id != second.Id
                    && affectedExtended.Add(sibling.Id))
                {
                    ApplyFamilySituationLoss(
                        sibling,
                        subject,
                        7,
                        extendedRelative: true);
                }
            }
        }
    }

    private void ApplyFamilySituationLoss(
        IPerson? person,
        IPerson eventRelative,
        double basePenalty,
        bool extendedRelative)
    {
        if (person is null
            || person.Tags.Has("state.dead")
            || SimulationState.IsInactive(person))
        {
            return;
        }

        var personHousehold =
            _economy.GetHouseholdId(person);

        var eventHousehold =
            _economy.GetHouseholdId(eventRelative);

        var penalty =
            FamilyShockRules.ScaleHealthLoss(
                person,
                basePenalty,
                personHousehold is not null
                    && eventHousehold is not null
                    && personHousehold == eventHousehold,
                extendedRelative);

        _health.ChangeHealth(
            person,
            -penalty);
    }

    private IReadOnlyList<IPerson> GetSiblings(
        IPerson person)
    {
        var result = new Dictionary<Guid, IPerson>();
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

    private void ApplyEmotionalHealthLoss(
        IPerson person,
        double basePenalty)
    {
        var penalty =
            basePenalty
            * PersonalityInfluence.Multiplier(
                person,
                melancholic: 0.15);

        _health.ChangeHealth(
            person,
            -penalty);
    }

    private string RandomWeightedSurname()
    {
        var entries =
            _data.GetWeightedStringList(
                SurnamesPath);

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
}
