using Dynastia.Contracts;

namespace Dynastia.Mechanics.Succession;

public sealed class StandardSuccessionService : ISuccessionService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly ISelectionService _selection;

    private bool _isGameOver;
    private int? _maleLineEndedYear;
    private Guid? _activeControllerId;

    public StandardSuccessionService(
        IGameState gameState,
        IFamilyService family,
        ISelectionService selection)
    {
        _gameState = gameState;
        _family = family;
        _selection = selection;
    }

    public bool IsGameOver => _isGameOver;
    public int? MaleLineEndedYear => _maleLineEndedYear;
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
            && _family.GetSex(person) == Sex.Male
            && _family.IsMaleLineage(person)
            && person.Age >= 18;
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
        var oldControllerId = _activeControllerId;

        RecalculateControllableTags();

        if (!HasLivingMaleLineage)
        {
            if (!_isGameOver)
            {
                _isGameOver = true;
                _maleLineEndedYear = _gameState.Year;
            }

            _activeControllerId = null;
        }
        else
        {
            _isGameOver = false;
            _maleLineEndedYear = null;

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

    private bool IsLivingMaleLineage(IPerson person)
    {
        return person.Tags.Has("state.alive")
            && _family.GetSex(person) == Sex.Male
            && _family.IsMaleLineage(person);
    }
}
