using System.Collections.ObjectModel;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IGameState _gameState;
    private readonly INewGameService _newGameService;
    private readonly YearProcessor _yearProcessor;
    private readonly ISelectionService _selectionService;
    private readonly IStatsService? _statsService;
    private readonly IFamilyService? _familyService;
    private readonly IHealthService? _healthService;
    private readonly IGameEventBus _eventBus;
    private readonly IActionRegistry _actionRegistry;

    private PersonRowViewModel? _selectedPerson;
    private FamilyDetailsViewModel? _selectedFamily;
    private HealthViewModel? _selectedHealth;
    private int _albumYear;
    private string _surnameInput = string.Empty;
    private bool _isGameStarted;

    public MainWindowViewModel(
        IGameState gameState,
        INewGameService newGameService,
        YearProcessor yearProcessor,
        ISelectionService selectionService,
        IStatsService? statsService,
        IFamilyService? familyService,
        IHealthService? healthService,
        IGameEventBus eventBus,
        IActionRegistry actionRegistry)
    {
        _gameState = gameState;
        _newGameService = newGameService;
        _yearProcessor = yearProcessor;
        _selectionService = selectionService;
        _statsService = statsService;
        _familyService = familyService;
        _healthService = healthService;
        _eventBus = eventBus;
        _actionRegistry = actionRegistry;

        _albumYear = 1900;

        StartGameCommand = new RelayCommand(StartGame);
        NextYearCommand = new RelayCommand(AdvanceYear);

        PreviousAlbumYearCommand = new RelayCommand(
            PreviousAlbumYear,
            () => IsGameStarted && AlbumYear > 1900);

        NextAlbumYearCommand = new RelayCommand(
            NextAlbumYear,
            () => IsGameStarted && AlbumYear < Year);

        _eventBus.EventPublished += OnEventPublished;
    }

    public string SurnameInput
    {
        get => _surnameInput;
        set
        {
            if (_surnameInput == value)
                return;

            _surnameInput = value;
            OnPropertyChanged();
        }
    }

    public bool IsGameStarted
    {
        get => _isGameStarted;
        private set
        {
            if (_isGameStarted == value)
                return;

            _isGameStarted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsStartScreenVisible));
        }
    }

    public bool IsStartScreenVisible => !IsGameStarted;

    public string DynastyTitle =>
        IsGameStarted
            ? $"The {_gameState.DynastySurname} Dynasty"
            : "Dynastia";

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
            RefreshFamilyDetails();
            RefreshHealth();
            RefreshActions();

            OnPropertyChanged();
        }
    }

    public FamilyDetailsViewModel? SelectedFamily
    {
        get => _selectedFamily;
        private set
        {
            _selectedFamily = value;
            OnPropertyChanged();
        }
    }

    public HealthViewModel? SelectedHealth
    {
        get => _selectedHealth;
        private set
        {
            _selectedHealth = value;
            OnPropertyChanged();
        }
    }

    public RelayCommand StartGameCommand { get; }
    public RelayCommand NextYearCommand { get; }
    public RelayCommand PreviousAlbumYearCommand { get; }
    public RelayCommand NextAlbumYearCommand { get; }

    private void StartGame()
    {
        _newGameService.StartNewGame(SurnameInput);

        IsGameStarted = true;
        AlbumYear = _gameState.Year;

        RefreshPeople();
        RefreshAlbum();

        OnPropertyChanged(nameof(Year));
        OnPropertyChanged(nameof(DynastyTitle));

        PreviousAlbumYearCommand.RaiseCanExecuteChanged();
        NextAlbumYearCommand.RaiseCanExecuteChanged();
    }

    private void AdvanceYear()
    {
        if (!IsGameStarted)
            return;

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
            People.Add(
                new PersonRowViewModel(
                    person,
                    _familyService,
                    _healthService));
        }

        SelectedPerson =
            selectedId is null
                ? People.FirstOrDefault()
                : People.FirstOrDefault(x => x.Id == selectedId)
                    ?? People.FirstOrDefault();

        RefreshSelectedStats();
        RefreshFamilyDetails();
        RefreshHealth();
        RefreshActions();
    }

    private void RefreshSelectedStats()
    {
        SelectedStats.Clear();

        if (_statsService is null)
            return;

        var person = FindSelectedPerson();

        if (person is null)
            return;

        foreach (var stat in _statsService.GetStats(person))
        {
            SelectedStats.Add(stat);
        }
    }

    private void RefreshHealth()
    {
        var person = FindSelectedPerson();

        if (person is null || _healthService is null)
        {
            SelectedHealth = null;
            return;
        }

        SelectedHealth =
            new HealthViewModel(
                _healthService.GetHealth(person));
    }

    private void RefreshFamilyDetails()
    {
        var person = FindSelectedPerson();

        if (person is null || _familyService is null)
        {
            SelectedFamily = null;
            return;
        }

        var father = _familyService.GetFather(person);
        var mother = _familyService.GetMother(person);
        var spouse = _familyService.GetSpouse(person);
        var children = _familyService.GetChildren(person);

        SelectedFamily = new FamilyDetailsViewModel
        {
            Sex = _familyService.GetSex(person).ToString(),

            Generation =
                _familyService.GetGeneration(person) is int generation
                    ? $"G{generation}"
                    : "N/A",

            Father = PersonName(father),
            Mother = PersonName(mother),
            Spouse = PersonName(spouse),

            Children =
                children.Count == 0
                    ? "None"
                    : string.Join(", ", children.Select(PersonName)),

            Bloodline =
                _familyService.IsBloodline(person)
                    ? "Yes"
                    : "No",

            MaleLineage =
                _familyService.IsMaleLineage(person)
                    ? "Yes"
                    : "No"
        };
    }

    private void RefreshActions()
    {
        AvailableActions.Clear();

        var person = FindSelectedPerson();

        if (person is not null)
        {
            foreach (var action in
                _actionRegistry.GetAvailableActions(
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
        RefreshHealth();
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

        foreach (var gameEvent in
            _eventBus.GetEventsForYear(AlbumYear))
        {
            AlbumEvents.Add(
                new AlbumEventViewModel(gameEvent));
        }

        OnPropertyChanged(nameof(AlbumEmptyText));
    }

    private void OnEventPublished(
        object? sender,
        GameEvent gameEvent)
    {
        if (gameEvent.Year == AlbumYear)
            RefreshAlbum();
    }

    private static string PersonName(IPerson? person)
    {
        return person is null
            ? "None"
            : $"{person.Name} {person.Surname}";
    }
}
