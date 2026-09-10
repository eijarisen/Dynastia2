using System.Text.RegularExpressions;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class StandardNewGameService : INewGameService
{
    private const string MaleNamesPath = "Names/polish_male.csv";
    private const string FemaleNamesPath = "Names/polish_female.csv";
    private const string SurnamesPath = "Names/polish_surnames.csv";

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly ISelectionService _selection;
    private readonly IGameDataService _data;
    private readonly IGameRandom _random;
    private readonly IGameCalendar _calendar;

    public StandardNewGameService(
        IGameState gameState,
        IFamilyService family,
        ISelectionService selection,
        IGameDataService data,
        IGameRandom random,
        IGameCalendar calendar)
    {
        _gameState = gameState;
        _family = family;
        _selection = selection;
        _data = data;
        _random = random;
        _calendar = calendar;
    }

    public IPerson StartNewGame(string dynastySurname)
    {
        var surname = NormalizeOrGenerateSurname(dynastySurname);

        _gameState.ClearPeople();
        _gameState.Year = 1900;
        _gameState.DynastySurname = surname;

        var father =
            _gameState.CreatePerson(
                RandomWeightedFrom(MaleNamesPath),
                surname,
                45);

        father.BirthDate =
            RandomDateInYear(
                _gameState.Year - father.Age);

        father.DeathDate =
            new GameDate(1899);

        _family.InitializePerson(
            father,
            Sex.Male,
            generation: 0);

        father.Tags.Add("state.dead");
        father.Tags.Add("family.bloodline");
        father.Tags.Add("sexuality.heterosexual");

        var mother =
            _gameState.CreatePerson(
                RandomWeightedFrom(FemaleNamesPath),
                surname,
                42);

        mother.MaidenName = surname;

        mother.BirthDate =
            RandomDateInYear(
                _gameState.Year - mother.Age);

        mother.DeathDate =
            new GameDate(1899);

        _family.InitializePerson(
            mother,
            Sex.Female);

        mother.Tags.Add("state.dead");
        mother.Tags.Add("sexuality.heterosexual");

        _family.SetSpouses(
            father,
            mother,
            startYear: 1880);

        _family.EndRelationship(
            father,
            mother,
            endYear: 1899,
            endReason: "death",
            clearFirst: false,
            clearSecond: false);

        var founder =
            _gameState.CreatePerson(
                RandomWeightedFrom(MaleNamesPath),
                surname,
                18);

        founder.BirthDate =
            RandomDateInYear(
                _gameState.Year - founder.Age);

        _family.InitializePerson(
            founder,
            Sex.Male,
            generation: 1);

        founder.Tags.Add("state.alive");
        founder.Tags.Add("age.adult");
        founder.Tags.Add("family.bloodline");
        founder.Tags.Add("lineage.male");
        founder.Tags.Add("control.playable");
        founder.Tags.Add("relationship.single");
        founder.Tags.Add("sexuality.heterosexual");

        _family.SetParents(
            founder,
            father,
            mother);

        _selection.SelectedPersonId =
            founder.Id;

        return founder;
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

    private string NormalizeOrGenerateSurname(string? input)
    {
        var cleaned = Regex.Replace(
            input ?? string.Empty,
            @"[^a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ\s]",
            string.Empty);

        if (cleaned.Length < 2)
            cleaned = RandomWeightedFrom(SurnamesPath);

        return char.ToUpperInvariant(cleaned[0])
            + cleaned[1..].ToLowerInvariant();
    }

    private string RandomWeightedFrom(
        string relativePath)
    {
        var entries =
            _data.GetWeightedStringList(
                relativePath);

        var totalWeight =
            entries.Sum(
                x => (double)x.Weight);

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
}
