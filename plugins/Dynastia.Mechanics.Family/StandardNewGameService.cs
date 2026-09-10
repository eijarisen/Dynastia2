using System.Text.RegularExpressions;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class StandardNewGameService : INewGameService
{
    private const string MaleNamesPath = "Names/polish_male.json";
    private const string FemaleNamesPath = "Names/polish_female.json";
    private const string SurnamesPath = "Names/polish_surnames.json";

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly ISelectionService _selection;
    private readonly IGameDataService _data;
    private readonly IGameRandom _random;

    public StandardNewGameService(
        IGameState gameState,
        IFamilyService family,
        ISelectionService selection,
        IGameDataService data,
        IGameRandom random)
    {
        _gameState = gameState;
        _family = family;
        _selection = selection;
        _data = data;
        _random = random;
    }

    public IPerson StartNewGame(string dynastySurname)
    {
        var surname = NormalizeOrGenerateSurname(dynastySurname);

        _gameState.ClearPeople();
        _gameState.Year = 1900;
        _gameState.DynastySurname = surname;

        var father =
            _gameState.CreatePerson(
                RandomFrom(MaleNamesPath),
                surname,
                45);

        _family.InitializePerson(
            father,
            Sex.Male,
            generation: 0);

        father.Tags.Add("state.dead");
        father.Tags.Add("family.bloodline");

        var mother =
            _gameState.CreatePerson(
                RandomFrom(FemaleNamesPath),
                surname,
                42);

        _family.InitializePerson(
            mother,
            Sex.Female);

        mother.Tags.Add("state.dead");

        _family.SetSpouses(father, mother);

        var founder =
            _gameState.CreatePerson(
                RandomFrom(MaleNamesPath),
                surname,
                18);

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

        _family.SetParents(
            founder,
            father,
            mother);

        _selection.SelectedPersonId = founder.Id;

        return founder;
    }

    private string NormalizeOrGenerateSurname(string? input)
    {
        var cleaned = Regex.Replace(
            input ?? string.Empty,
            @"[^a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ\s]",
            string.Empty);

        if (cleaned.Length < 2)
        {
            cleaned = RandomFrom(SurnamesPath);
        }

        if (cleaned.Length == 0)
            return RandomFrom(SurnamesPath);

        return char.ToUpperInvariant(cleaned[0])
            + cleaned[1..].ToLowerInvariant();
    }

    private string RandomFrom(string relativePath)
    {
        var values = _data.GetStringList(relativePath);

        return values[
            _random.NextInt(0, values.Count - 1)];
    }
}
