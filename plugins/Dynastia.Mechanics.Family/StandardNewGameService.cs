using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class StandardNewGameService : INewGameService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly ISelectionService _selection;

    public StandardNewGameService(
        IGameState gameState,
        IFamilyService family,
        ISelectionService selection)
    {
        _gameState = gameState;
        _family = family;
        _selection = selection;
    }

    public IPerson StartNewGame(string dynastySurname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dynastySurname);

        _gameState.ClearPeople();
        _gameState.Year = 1900;
        _gameState.DynastySurname = dynastySurname.Trim();

        // Temporary fixed names.
        // Name/data generation will be moved into the data system next.
        var father =
            _gameState.CreatePerson("Jan", dynastySurname, 45);

        _family.InitializePerson(
            father,
            Sex.Male,
            generation: 0);

        father.Tags.Add("state.dead");
        father.Tags.Add("family.bloodline");

        var mother =
            _gameState.CreatePerson("Anna", dynastySurname, 42);

        _family.InitializePerson(
            mother,
            Sex.Female);

        mother.Tags.Add("state.dead");

        _family.SetSpouses(father, mother);

        var founder =
            _gameState.CreatePerson("Piotr", dynastySurname, 18);

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
}
