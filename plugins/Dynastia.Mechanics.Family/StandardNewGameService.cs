using System.Text.RegularExpressions;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class StandardNewGameService : INewGameService
{
    private const string MaleNamesPath =
        "Names/polish_male.csv";

    private const string FemaleNamesPath =
        "Names/polish_female.csv";

    private const string SurnamesPath =
        "Names/polish_surnames.csv";

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly ISelectionService _selection;
    private readonly IGameDataService _data;
    private readonly IGameRandom _random;
    private readonly IGameCalendar _calendar;
    private readonly IGameEventBus _events;

    public StandardNewGameService(
        IGameState gameState,
        IFamilyService family,
        ISelectionService selection,
        IGameDataService data,
        IGameRandom random,
        IGameCalendar calendar,
        IGameEventBus events)
    {
        _gameState = gameState;
        _family = family;
        _selection = selection;
        _data = data;
        _random = random;
        _calendar = calendar;
        _events = events;
    }

    public IPerson StartNewGame(
        string dynastySurname,
        int startYear = GameCalendarConfiguration.GameStartYear)
    {
        var surname =
            NormalizeOrGenerateSurname(
                dynastySurname);

        startYear =
            GameCalendarConfiguration.NormalizeSelectableStartYear(
                startYear);

        _gameState.ClearPeople();
        _gameState.StartYear =
            startYear;
        _gameState.Year =
            startYear;
        _gameState.DynastySurname =
            surname;

        var parentDeathYear =
            startYear - 1;

        var parentDeathDate =
            RandomDateInYear(parentDeathYear);

        var father =
            _gameState.CreatePerson(
                RandomWeightedFrom(
                    MaleNamesPath),
                surname,
                45);

        father.BirthDate =
            RandomDateInYear(
                _gameState.Year
                - father.Age);

        father.DeathDate =
            parentDeathDate;

        _family.InitializePerson(
            father,
            Sex.Male,
            generation: 0);

        father.Tags.Add(
            "state.dead");

        father.Tags.Add(
            "family.bloodline");

        father.Tags.Add(
            "lineage.male");

        father.Tags.Add(
            "sexuality.heterosexual");

        var mother =
            _gameState.CreatePerson(
                RandomWeightedFrom(
                    FemaleNamesPath),
                surname,
                42);

        mother.MaidenName =
            RandomWeightedDifferentFrom(
                SurnamesPath,
                surname);

        mother.BirthDate =
            RandomDateInYear(
                _gameState.Year
                - mother.Age);

        mother.DeathDate =
            parentDeathDate;

        _family.InitializePerson(
            mother,
            Sex.Female,
            generation: 0);

        mother.Tags.Add(
            "state.dead");

        mother.Tags.Add(
            "family.bloodline");

        mother.Tags.Add(
            "sexuality.heterosexual");

        _family.SetSpouses(
            father,
            mother,
            startYear:
                startYear - 20);

        _family.EndRelationship(
            father,
            mother,
            endYear: parentDeathYear,
            endReason: "death",
            clearFirst: false,
            clearSecond: false);

        var olderSibling =
            _gameState.CreatePerson(
                RandomWeightedDifferentFrom(
                    FemaleNamesPath,
                    mother.Name),
                surname,
                _random.NextInt(20, 24));

        olderSibling.MaidenName =
            surname;

        olderSibling.BirthDate =
            RandomDateInYear(
                _gameState.Year
                - olderSibling.Age);

        _family.InitializePerson(
            olderSibling,
            Sex.Female,
            generation: 1);

        olderSibling.Tags.Add(
            "state.alive");

        olderSibling.Tags.Add(
            "age.adult");

        olderSibling.Tags.Add(
            "family.bloodline");

        olderSibling.Tags.Add(
            "relationship.single");

        olderSibling.Tags.Add(
            "sexuality.heterosexual");

        _family.SetParents(
            olderSibling,
            father,
            mother);

        var founder =
            _gameState.CreatePerson(
                RandomWeightedFrom(
                    MaleNamesPath),
                surname,
                18);

        founder.BirthDate =
            RandomDateInYear(
                _gameState.Year
                - founder.Age);

        _family.InitializePerson(
            founder,
            Sex.Male,
            generation: 1);

        founder.Tags.Add(
            "state.alive");

        founder.Tags.Add(
            "age.adult");

        founder.Tags.Add(
            "family.bloodline");

        founder.Tags.Add(
            "lineage.male");

        founder.Tags.Add(
            "relationship.single");

        founder.Tags.Add(
            "sexuality.heterosexual");

        _family.SetParents(
            founder,
            father,
            mother);

        _selection.SelectedPersonId =
            founder.Id;

        _events.Publish(
            new GameEvent
            {
                Type = "game.started",
                Year = _gameState.Year,
                SubjectId = founder.Id,

                RelatedPersonIds =
                    [
                        father.Id,
                        mother.Id,
                        olderSibling.Id
                    ],

                Data =
                    new Dictionary<string, string>
                    {
                        ["surname"] = surname,
                        ["text"] =
                            $"The {surname} dynasty began."
                    }
            });

        return founder;
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

    private string NormalizeOrGenerateSurname(
        string? input)
    {
        var cleaned =
            Regex.Replace(
                input
                    ?? string.Empty,
                @"[^a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ\s]",
                string.Empty);

        if (cleaned.Length < 2)
        {
            cleaned =
                RandomWeightedFrom(
                    SurnamesPath);
        }

        return char.ToUpperInvariant(
                cleaned[0])
            + cleaned[1..]
                .ToLowerInvariant();
    }

    private string RandomWeightedDifferentFrom(
        string relativePath,
        string excludedValue)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var candidate =
                RandomWeightedFrom(
                    relativePath);

            if (!candidate.Equals(
                    excludedValue,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return _data
            .GetWeightedStringList(
                relativePath)
            .Select(entry => entry.Value)
            .First(candidate =>
                !candidate.Equals(
                    excludedValue,
                    StringComparison.OrdinalIgnoreCase));
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

        foreach (var entry in entries)
        {
            if (roll < entry.Weight)
                return entry.Value;

            roll -=
                entry.Weight;
        }

        return entries[^1].Value;
    }
}
