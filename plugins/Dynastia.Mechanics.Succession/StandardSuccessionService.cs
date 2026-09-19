using Dynastia.Contracts;

namespace Dynastia.Mechanics.Succession;

public sealed class StandardSuccessionService : ISuccessionService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly ISelectionService _selection;

    private bool _isGameOver;
    private int? _maleLineEndedYear;
    private bool _dynastyLeftPoland;
    private int? _dynastyLeftPolandYear;
    private Guid? _activeControllerId;

    public StandardSuccessionService(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        ISelectionService selection)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
        _selection = selection;
    }

    public bool IsGameOver => _isGameOver;
    public int? MaleLineEndedYear => _maleLineEndedYear;
    public bool DynastyLeftPoland => _dynastyLeftPoland;
    public int? DynastyLeftPolandYear => _dynastyLeftPolandYear;
    public Guid? ActiveControllerId => _activeControllerId;

    public IPerson? ActiveController =>
        _activeControllerId is Guid id
            ? _gameState.People.FirstOrDefault(person => person.Id == id)
            : null;

    public bool HasLivingMaleLineage =>
        _gameState.People.Any(IsLivingMaleLineage);

    public event EventHandler? StateChanged;

    public bool IsControllable(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return person.Tags.Has("state.alive")
            && !SimulationState.IsExternallyResident(person)
            && _family.GetSex(person) == Sex.Male
            && _family.IsMaleLineage(person)
            && person.Age >= 18
            && _economy.HasHousehold(person);
    }

    public bool SetActiveController(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (_isGameOver || !IsControllable(person))
            return false;

        if (_activeControllerId == person.Id)
        {
            _selection.SelectedPersonId = person.Id;
            return true;
        }

        _activeControllerId = person.Id;
        _selection.SelectedPersonId = person.Id;
        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Refresh()
    {
        var oldGameOver = _isGameOver;
        var oldEndedYear = _maleLineEndedYear;
        var oldLeftPoland = _dynastyLeftPoland;
        var oldLeftPolandYear = _dynastyLeftPolandYear;
        var oldControllerId = _activeControllerId;

        RecalculateControllableTags();

        if (!HasLivingMaleLineage)
        {
            if (!_isGameOver || _dynastyLeftPoland)
            {
                _isGameOver = true;
                _maleLineEndedYear = _gameState.Year;
            }

            _dynastyLeftPoland = false;
            _dynastyLeftPolandYear = null;
            _activeControllerId = null;
        }
        else if (!HasLivingMaleLineageInPoland())
        {
            _isGameOver = true;
            _maleLineEndedYear = null;
            _dynastyLeftPoland = true;
            _dynastyLeftPolandYear ??= _gameState.Year;
            _activeControllerId = null;
        }
        else
        {
            _isGameOver = false;
            _maleLineEndedYear = null;
            _dynastyLeftPoland = false;
            _dynastyLeftPolandYear = null;

            var current = ActiveController;

            if (current is null || !IsControllable(current))
            {
                _activeControllerId =
                    _gameState.People.FirstOrDefault(IsControllable)?.Id;
            }
        }

        if (oldControllerId != _activeControllerId
            && _activeControllerId is Guid newControllerId)
        {
            _selection.SelectedPersonId = newControllerId;
        }

        if (oldGameOver != _isGameOver
            || oldEndedYear != _maleLineEndedYear
            || oldLeftPoland != _dynastyLeftPoland
            || oldLeftPolandYear != _dynastyLeftPolandYear
            || oldControllerId != _activeControllerId)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RecalculateControllableTags()
    {
        foreach (var person in _gameState.People)
        {
            person.Tags.Remove("control.playable");

            if (IsControllable(person))
                person.Tags.Add("control.playable");
        }
    }


    private bool HasLivingMaleLineageInPoland() =>
        _gameState.People.Any(person =>
            IsLivingMaleLineage(person)
            && !SimulationState.IsExternallyResident(person));

    private bool IsLivingMaleLineage(IPerson person)
    {
        // External residence removes local controllability, but the person is
        // still alive and still belongs to the male lineage.  Keeping those
        // concepts separate is what lets Refresh distinguish extinction from
        // the historical-events terminal state where the dynasty has left
        // Poland.
        return person.Tags.Has("state.alive")
            && _family.GetSex(person) == Sex.Male
            && _family.IsMaleLineage(person);
    }
}
