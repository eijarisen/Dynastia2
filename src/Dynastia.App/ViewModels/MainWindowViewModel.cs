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
    private readonly IEconomyService? _economyService;
    private readonly ISuccessionService _succession;
    private readonly IGameEventBus _eventBus;
    private readonly IActionRegistry _actionRegistry;

    private PersonRowViewModel? _selectedPerson;
    private FamilyDetailsViewModel? _selectedFamily;
    private HealthViewModel? _selectedHealth;
    private EconomyViewModel? _selectedEconomy;

    private int _albumYear;
    private string _surnameInput = string.Empty;
    private bool _isGameStarted;
    private string _queuedActionText = string.Empty;
    private bool _hasQueuedAction;
    private bool _isGameOverOverlayVisible;

    public MainWindowViewModel(
        IGameState gameState,
        INewGameService newGameService,
        YearProcessor yearProcessor,
        ISelectionService selectionService,
        IStatsService? statsService,
        IFamilyService? familyService,
        IHealthService? healthService,
        IEconomyService? economyService,
        ISuccessionService succession,
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
        _economyService = economyService;
        _succession = succession;
        _eventBus = eventBus;
        _actionRegistry = actionRegistry;

        _albumYear = 1900;

        StartGameCommand =
            new RelayCommand(StartGame);

        NextYearCommand =
            new RelayCommand(
                AdvanceYear,
                () =>
                    IsGameStarted
                    && !_succession.IsGameOver);

        CancelQueuedActionCommand =
            new RelayCommand(
                CancelQueuedAction);

        GoBackFromGameOverCommand =
            new RelayCommand(
                HideGameOver);

        PreviousAlbumYearCommand =
            new RelayCommand(
                PreviousAlbumYear,
                () =>
                    IsGameStarted
                    && AlbumYear > 1900);

        NextAlbumYearCommand =
            new RelayCommand(
                NextAlbumYear,
                () =>
                    IsGameStarted
                    && AlbumYear < Year);

        _eventBus.EventPublished +=
            OnEventPublished;

        _succession.StateChanged +=
            OnSuccessionStateChanged;
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
            OnPropertyChanged(
                nameof(IsStartScreenVisible));

            NextYearCommand
                .RaiseCanExecuteChanged();
        }
    }

    public bool IsStartScreenVisible =>
        !IsGameStarted;

    public string DynastyTitle =>
        IsGameStarted
            ? $"The {_gameState.DynastySurname} Dynasty"
            : "Dynastia";

    public int Year =>
        _gameState.Year;

    public bool IsGameOver =>
        _succession.IsGameOver;

    public bool IsGameOverOverlayVisible
    {
        get =>
            _isGameOverOverlayVisible;

        private set
        {
            if (_isGameOverOverlayVisible
                == value)
            {
                return;
            }

            _isGameOverOverlayVisible =
                value;

            OnPropertyChanged();
        }
    }

    public string GameOverTitle =>
        "Dynasty collapsed";

    public string GameOverText
    {
        get
        {
            var year =
                _succession.MaleLineEndedYear
                ?? _gameState.Year;

            return
                $"The male line of the " +
                $"{_gameState.DynastySurname} dynasty " +
                $"ended in {year}.";
        }
    }

    public int AlbumYear
    {
        get => _albumYear;
        private set
        {
            if (_albumYear == value)
                return;

            _albumYear = value;
            OnPropertyChanged();

            PreviousAlbumYearCommand
                .RaiseCanExecuteChanged();

            NextAlbumYearCommand
                .RaiseCanExecuteChanged();

            RefreshAlbum();
        }
    }

    public string AlbumEmptyText =>
        AlbumEvents.Count == 0
            ? "Nothing of note happened this year."
            : string.Empty;

    public string ActionsEmptyText =>
        AvailableActions.Count == 0
        && !HasQueuedAction
            ? "No actions available for the selected person."
            : string.Empty;

    public string QueuedActionText
    {
        get => _queuedActionText;
        private set
        {
            if (_queuedActionText == value)
                return;

            _queuedActionText = value;
            OnPropertyChanged();
        }
    }

    public bool HasQueuedAction
    {
        get => _hasQueuedAction;
        private set
        {
            if (_hasQueuedAction == value)
                return;

            _hasQueuedAction = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<AlbumEventViewModel>
        AlbumEvents { get; } = [];

    public ObservableCollection<PersonRowViewModel>
        People { get; } = [];

    public ObservableCollection<StatValue>
        SelectedStats { get; } = [];

    public ObservableCollection<AvailableActionViewModel>
        AvailableActions { get; } = [];

    public PersonRowViewModel? SelectedPerson
    {
        get => _selectedPerson;
        set
        {
            if (ReferenceEquals(
                _selectedPerson,
                value))
            {
                return;
            }

            _selectedPerson = value;

            _selectionService.SelectedPersonId =
                value?.Id;

            RefreshSelectedStats();
            RefreshFamilyDetails();
            RefreshHealth();
            RefreshEconomy();
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

    public EconomyViewModel? SelectedEconomy
    {
        get => _selectedEconomy;
        private set
        {
            _selectedEconomy = value;
            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasSelectedEconomy));
        }
    }

    public bool HasSelectedEconomy =>
        SelectedEconomy is not null;

    public RelayCommand StartGameCommand { get; }
    public RelayCommand NextYearCommand { get; }
    public RelayCommand CancelQueuedActionCommand { get; }
    public RelayCommand GoBackFromGameOverCommand { get; }
    public RelayCommand PreviousAlbumYearCommand { get; }
    public RelayCommand NextAlbumYearCommand { get; }

    private void StartGame()
    {
        _newGameService.StartNewGame(
            SurnameInput);

        _succession.Refresh();

        IsGameOverOverlayVisible =
            false;

        IsGameStarted = true;
        AlbumYear = _gameState.Year;

        RefreshPeople();
        RefreshAlbum();

        NotifyGameStateChanged();
    }

    private void AdvanceYear()
    {
        if (!IsGameStarted
            || _succession.IsGameOver)
        {
            return;
        }

        _yearProcessor.AdvanceYear();

        AlbumYear =
            _gameState.Year;

        RefreshPeople();

        NotifyGameStateChanged();
    }

    private void CancelQueuedAction()
    {
        var person =
            FindSelectedPerson();

        if (person is null)
            return;

        _actionRegistry
            .CancelQueuedActions(
                person);

        RefreshActions();
    }

    private void HideGameOver()
    {
        IsGameOverOverlayVisible =
            false;
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
        var selectedId =
            _selectionService.SelectedPersonId;

        People.Clear();

        foreach (var person in
            _gameState.People)
        {
            People.Add(
                new PersonRowViewModel(
                    person,
                    _familyService,
                    _healthService,
                    _economyService));
        }

        SelectedPerson =
            selectedId is null
                ? People.FirstOrDefault()
                : People.FirstOrDefault(
                    x => x.Id == selectedId)
                    ?? People.FirstOrDefault();

        RefreshSelectedStats();
        RefreshFamilyDetails();
        RefreshHealth();
        RefreshEconomy();
        RefreshActions();
    }

    private void RefreshSelectedStats()
    {
        SelectedStats.Clear();

        if (_statsService is null)
            return;

        var person =
            FindSelectedPerson();

        if (person is null)
            return;

        foreach (var stat in
            _statsService.GetStats(
                person))
        {
            SelectedStats.Add(
                stat);
        }
    }

    private void RefreshHealth()
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _healthService is null)
        {
            SelectedHealth = null;
            return;
        }

        SelectedHealth =
            new HealthViewModel(
                _healthService.GetHealth(
                    person));
    }

    private void RefreshEconomy()
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _economyService is null)
        {
            SelectedEconomy = null;
            return;
        }

        var snapshot =
            _economyService.GetHousehold(
                person);

        SelectedEconomy =
            snapshot is null
                ? null
                : new EconomyViewModel(
                    snapshot);
    }

    private void RefreshFamilyDetails()
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _familyService is null)
        {
            SelectedFamily = null;
            return;
        }

        var generatedBackground =
            _familyService
                .GetGeneratedFamilyBackground(
                    person);

        var father =
            _familyService.GetFather(
                person);

        var mother =
            _familyService.GetMother(
                person);

        var spouse =
            _familyService.GetSpouse(
                person);

        var children =
            _familyService.GetChildren(
                person);

        var siblings =
            generatedBackground is null
                ? GetSimulatedSiblings(
                    person,
                    father)
                : [];

        var relationshipHistory =
            _familyService
                .GetRelationshipHistory(
                    person);

        SelectedFamily =
            new FamilyDetailsViewModel
            {
                Sex =
                    _familyService
                        .GetSex(person)
                        .ToString(),

                Generation =
                    _familyService
                        .GetGeneration(person)
                        is int generation
                            ? $"G{generation}"
                            : "N/A",

                Father =
                    generatedBackground?.FatherName
                    ?? PersonName(father),

                Mother =
                    generatedBackground?.MotherName
                    ?? PersonName(mother),

                Siblings =
                    generatedBackground is not null
                        ? FormatNames(
                            generatedBackground.Siblings)
                        : FormatPeople(
                            siblings),

                Spouse =
                    PersonName(spouse),

                Children =
                    FormatPeople(
                        children),

                RelationshipHistory =
                    FormatRelationshipHistory(
                        relationshipHistory),

                Bloodline =
                    _familyService
                        .IsBloodline(person)
                            ? "Yes"
                            : "No",

                MaleLineage =
                    _familyService
                        .IsMaleLineage(person)
                            ? "Yes"
                            : "No"
            };
    }

    private IReadOnlyList<IPerson>
        GetSimulatedSiblings(
            IPerson person,
            IPerson? father)
    {
        if (father is null
            || _familyService is null)
        {
            return [];
        }

        return _familyService
            .GetChildren(father)
            .Where(
                sibling =>
                    sibling.Id
                    != person.Id)
            .ToList();
    }

    private void RefreshActions()
    {
        AvailableActions.Clear();

        var person =
            FindSelectedPerson();

        HasQueuedAction = false;
        QueuedActionText =
            string.Empty;

        if (person is not null
            && !_succession.IsGameOver)
        {
            var queued =
                _actionRegistry
                    .GetQueuedActions(
                        person);

            if (queued.Count > 0)
            {
                HasQueuedAction =
                    true;

                QueuedActionText =
                    $"Queued: " +
                    $"{queued[0].Label}";
            }
            else
            {
                foreach (var action in
                    _actionRegistry
                        .GetAvailableActions(
                            person,
                            person))
                {
                    var actionId =
                        action.Id;

                    AvailableActions.Add(
                        new AvailableActionViewModel(
                            action,
                            () =>
                                ExecuteAction(
                                    actionId)));
                }
            }
        }

        OnPropertyChanged(
            nameof(ActionsEmptyText));
    }

    private void ExecuteAction(
        string actionId)
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _succession.IsGameOver)
        {
            return;
        }

        _actionRegistry.Execute(
            actionId,
            person,
            person);

        RefreshPeople();
        RefreshAlbum();
        RefreshHealth();
        RefreshEconomy();
        RefreshActions();
    }

    private IPerson? FindSelectedPerson()
    {
        if (SelectedPerson is null)
            return null;

        return _gameState.People
            .FirstOrDefault(
                x =>
                    x.Id
                    == SelectedPerson.Id);
    }

    private void RefreshAlbum()
    {
        AlbumEvents.Clear();

        foreach (var gameEvent in
            _eventBus.GetEventsForYear(
                AlbumYear))
        {
            AlbumEvents.Add(
                new AlbumEventViewModel(
                    gameEvent));
        }

        OnPropertyChanged(
            nameof(AlbumEmptyText));
    }

    private void OnEventPublished(
        object? sender,
        GameEvent gameEvent)
    {
        if (gameEvent.Year
            == AlbumYear)
        {
            RefreshAlbum();
        }
    }

    private void OnSuccessionStateChanged(
        object? sender,
        EventArgs e)
    {
        if (_succession.IsGameOver)
        {
            IsGameOverOverlayVisible =
                true;
        }

        NextYearCommand
            .RaiseCanExecuteChanged();

        OnPropertyChanged(
            nameof(IsGameOver));

        OnPropertyChanged(
            nameof(GameOverText));
    }

    private void NotifyGameStateChanged()
    {
        OnPropertyChanged(
            nameof(Year));

        OnPropertyChanged(
            nameof(DynastyTitle));

        OnPropertyChanged(
            nameof(IsGameOver));

        OnPropertyChanged(
            nameof(GameOverText));

        PreviousAlbumYearCommand
            .RaiseCanExecuteChanged();

        NextAlbumYearCommand
            .RaiseCanExecuteChanged();

        NextYearCommand
            .RaiseCanExecuteChanged();
    }

    private string PersonName(
        IPerson? person)
    {
        if (person is null)
            return "None";

        return _familyService is null
            ? $"{person.Name} " +
              $"{person.Surname}"
            : _familyService
                .GetDisplayName(
                    person);
    }

    private string FormatPeople(
        IReadOnlyList<IPerson> people)
    {
        return people.Count == 0
            ? "None"
            : string.Join(
                ", ",
                people.Select(
                    PersonName));
    }

    private static string FormatNames(
        IReadOnlyList<string> names)
    {
        return names.Count == 0
            ? "None"
            : string.Join(
                ", ",
                names);
    }

    private string FormatRelationshipHistory(
        IReadOnlyList<
            RelationshipHistoryInfo>
            history)
    {
        if (history.Count == 0)
            return "None";

        var lines =
            new List<string>();

        foreach (var relationship in
            history.OrderBy(
                x => x.StartYear))
        {
            var spouse =
                _gameState.People
                    .FirstOrDefault(
                        x =>
                            x.Id
                            == relationship.SpouseId);

            var spouseName =
                PersonName(
                    spouse);

            var end =
                relationship.EndYear
                    is int endYear
                        ? endYear.ToString()
                        : "present";

            var reason =
                string.IsNullOrWhiteSpace(
                    relationship.EndReason)
                    ? string.Empty
                    : $" ({relationship.EndReason})";

            lines.Add(
                $"{relationship.StartYear}–{end}: " +
                $"{spouseName}{reason}");
        }

        return string.Join(
            Environment.NewLine,
            lines);
    }
}
