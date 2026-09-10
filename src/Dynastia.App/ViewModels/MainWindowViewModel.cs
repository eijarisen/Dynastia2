using System.Collections.ObjectModel;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IGameState _gameState;
    private readonly YearProcessor _yearProcessor;
    private readonly ISelectionService _selectionService;
    private readonly IStatsService? _statsService;
    private readonly IGameEventBus _eventBus;
    private readonly IActionRegistry _actionRegistry;

    private PersonRowViewModel? _selectedPerson;
    private int _albumYear;

    public MainWindowViewModel(
        IGameState gameState,
        YearProcessor yearProcessor,
        ISelectionService selectionService,
        IStatsService? statsService,
        IGameEventBus eventBus,
        IActionRegistry actionRegistry)
    {
        _gameState = gameState;
        _yearProcessor = yearProcessor;
        _selectionService = selectionService;
        _statsService = statsService;
        _eventBus = eventBus;
        _actionRegistry = actionRegistry;

        _albumYear = gameState.Year;

        NextYearCommand = new RelayCommand(AdvanceYear);
        PreviousAlbumYearCommand = new RelayCommand(
            PreviousAlbumYear,
            () => AlbumYear > 1900);
        NextAlbumYearCommand = new RelayCommand(
            NextAlbumYear,
            () => AlbumYear < Year);

        _eventBus.EventPublished += OnEventPublished;

        RefreshPeople();
        RefreshAlbum();
    }

    public string DynastyTitle =>
        $"The {_gameState.DynastySurname} Dynasty";

    public int Year => _gameState.Year;

    public int AlbumYear
    {
        get => _albumYear;
        private set
        {
            if (_albumYear == value)
                return;

            _albumYear = value;
            OnPropertyChanged();

            PreviousAlbumYearCommand.RaiseCanExecuteChanged();
            NextAlbumYearCommand.RaiseCanExecuteChanged();

            RefreshAlbum();
        }
    }

    public string AlbumEmptyText =>
        AlbumEvents.Count == 0
            ? "Nothing of note happened this year."
            : string.Empty;

    public string ActionsEmptyText =>
        AvailableActions.Count == 0
            ? "No actions available for the selected person."
            : string.Empty;

    public ObservableCollection<AlbumEventViewModel> AlbumEvents { get; } = [];
    public ObservableCollection<PersonRowViewModel> People { get; } = [];
    public ObservableCollection<StatValue> SelectedStats { get; } = [];
    public ObservableCollection<AvailableActionViewModel> AvailableActions { get; } = [];

    public PersonRowViewModel? SelectedPerson
    {
        get => _selectedPerson;
        set
        {
            if (ReferenceEquals(_selectedPerson, value))
                return;

            _selectedPerson = value;
            _selectionService.SelectedPersonId = value?.Id;

            RefreshSelectedStats();
            RefreshActions();

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedPerson));
        }
    }

    public bool HasSelectedPerson => SelectedPerson is not null;

    public RelayCommand NextYearCommand { get; }
    public RelayCommand PreviousAlbumYearCommand { get; }
    public RelayCommand NextAlbumYearCommand { get; }

    private void AdvanceYear()
    {
        _yearProcessor.AdvanceYear();

        AlbumYear = _gameState.Year;

        RefreshPeople();

        OnPropertyChanged(nameof(Year));
        OnPropertyChanged(nameof(DynastyTitle));

        PreviousAlbumYearCommand.RaiseCanExecuteChanged();
        NextAlbumYearCommand.RaiseCanExecuteChanged();
    }

    private void PreviousAlbumYear()
    {
        if (AlbumYear > 1900)
            AlbumYear--;
    }

    private void NextAlbumYear()
    {
        if (AlbumYear < Year)
            AlbumYear++;
    }

    private void RefreshPeople()
    {
        var selectedId = _selectionService.SelectedPersonId;

        People.Clear();

        foreach (var person in _gameState.People)
        {
            People.Add(new PersonRowViewModel(person));
        }

        SelectedPerson =
            selectedId is null
                ? People.FirstOrDefault()
                : People.FirstOrDefault(x => x.Id == selectedId)
                    ?? People.FirstOrDefault();

        RefreshSelectedStats();
        RefreshActions();
    }

    private void RefreshSelectedStats()
    {
        SelectedStats.Clear();

        if (_statsService is null || SelectedPerson is null)
            return;

        var person = FindSelectedPerson();

        if (person is null)
            return;

        foreach (var stat in _statsService.GetStats(person))
        {
            SelectedStats.Add(stat);
        }
    }

    private void RefreshActions()
    {
        AvailableActions.Clear();

        var person = FindSelectedPerson();

        if (person is not null)
        {
            foreach (var action in _actionRegistry.GetAvailableActions(
                person,
                person))
            {
                var actionId = action.Id;

                AvailableActions.Add(
                    new AvailableActionViewModel(
                        action,
                        () => ExecuteAction(actionId)));
            }
        }

        OnPropertyChanged(nameof(ActionsEmptyText));
    }

    private void ExecuteAction(string actionId)
    {
        var person = FindSelectedPerson();

        if (person is null)
            return;

        _actionRegistry.Execute(
            actionId,
            person,
            person);

        RefreshPeople();
        RefreshAlbum();
        RefreshActions();
    }

    private IPerson? FindSelectedPerson()
    {
        if (SelectedPerson is null)
            return null;

        return _gameState.People.FirstOrDefault(
            x => x.Id == SelectedPerson.Id);
    }

    private void RefreshAlbum()
    {
        AlbumEvents.Clear();

        foreach (var gameEvent in _eventBus.GetEventsForYear(AlbumYear))
        {
            AlbumEvents.Add(new AlbumEventViewModel(gameEvent));
        }

        OnPropertyChanged(nameof(AlbumEmptyText));
    }

    private void OnEventPublished(object? sender, GameEvent gameEvent)
    {
        if (gameEvent.Year == AlbumYear)
            RefreshAlbum();
    }
}
