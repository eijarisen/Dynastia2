using System.Collections.ObjectModel;
using Avalonia.Threading;
using Dynastia.App.Persistence;
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
    private readonly IHouseholdService? _householdService;
    private readonly IAdoptionService? _adoptionService;
    private readonly ILocationService? _locationService;
    private readonly IMarriageSatisfactionService?
        _marriageSatisfactionService;
    private readonly IThoughtService? _thoughtService;
    private readonly IPersonalityService? _personalityService;
    private readonly IEducationService? _educationService;
    private readonly ICareerService? _careerService;
    private readonly IJusticeService? _justiceService;
    private readonly IBiographyService? _biographyService;
    private readonly ISuccessionService _succession;
    private readonly IGameEventBus _eventBus;
    private readonly IActionRegistry _actionRegistry;
    private readonly GameSaveService _saveService;

    private PersonRowViewModel? _selectedPerson;
    private FamilyDetailsViewModel? _selectedFamily;
    private HealthViewModel? _selectedHealth;
    private EconomyViewModel? _selectedEconomy;
    private EducationViewModel? _selectedEducation;
    private CareerViewModel? _selectedCareer;
    private JusticeViewModel? _selectedJustice;
    private string _selectedAboutText =
        "No information available.";

    private int _albumYear;
    private string _surnameInput = string.Empty;
    private bool _isGameStarted;
    private string _queuedActionText = string.Empty;
    private bool _hasQueuedAction;
    private bool _isGameOverOverlayVisible;
    private bool _isMainMenuPromptVisible;
    private HouseholdViewMode _householdViewMode =
        HouseholdViewMode.Lineage;

    private Guid? _inspectedBloodlineHouseholdId;

    private int _detailsTabIndex;
    private string _persistenceStatusText = string.Empty;
    private CancellationTokenSource? _loadedStatusCancellation;

    public MainWindowViewModel(
        IGameState gameState,
        INewGameService newGameService,
        YearProcessor yearProcessor,
        ISelectionService selectionService,
        IStatsService? statsService,
        IFamilyService? familyService,
        IHealthService? healthService,
        IEconomyService? economyService,
        IHouseholdService? householdService,
        IAdoptionService? adoptionService,
        ILocationService? locationService,
        IMarriageSatisfactionService? marriageSatisfactionService,
        IThoughtService? thoughtService,
        IPersonalityService? personalityService,
        IEducationService? educationService,
        ICareerService? careerService,
        IJusticeService? justiceService,
        IBiographyService? biographyService,
        ISuccessionService succession,
        IGameEventBus eventBus,
        IActionRegistry actionRegistry,
        GameSaveService saveService)
    {
        _gameState = gameState;
        _newGameService = newGameService;
        _yearProcessor = yearProcessor;
        _selectionService = selectionService;
        _statsService = statsService;
        _familyService = familyService;
        _healthService = healthService;
        _economyService = economyService;
        _householdService = householdService;
        _adoptionService = adoptionService;
        _locationService = locationService;
        _marriageSatisfactionService =
            marriageSatisfactionService;
        _thoughtService =
            thoughtService;
        _personalityService =
            personalityService;
        _educationService = educationService;
        _careerService = careerService;
        _justiceService = justiceService;
        _biographyService = biographyService;
        _succession = succession;
        _eventBus = eventBus;
        _actionRegistry = actionRegistry;
        _saveService = saveService;

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

        ReturnToMainMenuCommand =
            new RelayCommand(
                ReturnToMainMenu);

        ShowMainMenuPromptCommand =
            new RelayCommand(
                ShowMainMenuPrompt,
                () => IsGameStarted);

        HideMainMenuPromptCommand =
            new RelayCommand(
                HideMainMenuPrompt);

        ShowLivingFamilyCommand =
            new RelayCommand(
                ShowLivingFamily);

        ShowBloodlineFamilyCommand =
            new RelayCommand(
                ShowBloodlineFamily);

        ShowDeceasedFamilyCommand =
            new RelayCommand(
                ShowDeceasedFamily);

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

        _selectionService.SelectionChanged +=
            OnSelectionChanged;

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

            ShowMainMenuPromptCommand
                .RaiseCanExecuteChanged();
        }
    }

    public bool IsStartScreenVisible =>
        !IsGameStarted;

    public string DynastyTitle =>
        IsGameStarted
            ? $"The {_gameState.DynastySurname} Dynasty"
            : "Dynastia";

    public string SelectedPersonEmoji
    {
        get
        {
            var person =
                FindSelectedPerson();

            return person is null
                ? string.Empty
                : PersonEmojiResolver.GetPersonEmoji(
                    person,
                    _familyService,
                    _healthService,
                    _careerService,
                    _justiceService,
                    _statsService,
                    _thoughtService);
        }
    }

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

    public bool IsMainMenuPromptVisible
    {
        get =>
            _isMainMenuPromptVisible;

        private set
        {
            if (_isMainMenuPromptVisible
                == value)
            {
                return;
            }

            _isMainMenuPromptVisible =
                value;

            OnPropertyChanged();
        }
    }

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

    public string ActiveHouseholdText
    {
        get
        {
            var head =
                GetDisplayedHouseholdHead();

            if (head is null)
                return "No active household";

            var name =
                _familyService is null
                    ? $"{head.Name} {head.Surname}"
                    : _familyService.GetDisplayName(
                        head);

            return IsBloodlineFamilyView
                ? $"Independent household: {name}"
                : $"Active household: {name}";
        }
    }

    public bool IsLivingFamilyView
    {
        get =>
            _householdViewMode
            != HouseholdViewMode.Deceased;

        private set
        {
            SetHouseholdViewMode(
                value
                    ? HouseholdViewMode.Lineage
                    : HouseholdViewMode.Deceased);
        }
    }

    public bool IsLineageFamilyView =>
        _householdViewMode
        == HouseholdViewMode.Lineage;

    public bool IsBloodlineFamilyView =>
        _householdViewMode
        == HouseholdViewMode.Bloodline;

    public bool IsDeceasedFamilyView =>
        _householdViewMode
        == HouseholdViewMode.Deceased;

    public bool HasActiveHousehold =>
        GetDisplayedHouseholdHead()
        is not null;


    public string HouseholdBudgetText
    {
        get
        {
            var finance =
                GetDisplayedHouseholdFinance();

            return finance is null
                ? "No active adult household."
                : $"Family Budget: " +
                  $"{finance.Wealth:N0} zł";
        }
    }

    public string HouseholdIncomeText
    {
        get
        {
            var finance =
                GetDisplayedHouseholdFinance();

            return finance is null
                ? string.Empty
                : $"Income: " +
                  $"{finance.LastIncome:N0} zł";
        }
    }

    public string HouseholdIncomeDetailsText
    {
        get
        {
            var finance =
                GetDisplayedHouseholdFinance();

            if (finance is null)
                return string.Empty;

            return FormatFinanceBreakdown(
                finance.LastIncomeBreakdown,
                "No income was recorded in the last annual finance pass.");
        }
    }

    public string HouseholdExpensesText
    {
        get
        {
            var finance =
                GetDisplayedHouseholdFinance();

            return finance is null
                ? string.Empty
                : $"Expenses: " +
                  $"{finance.LastExpenses:N0} zł";
        }
    }

    public string HouseholdExpenseDetailsText
    {
        get
        {
            var finance =
                GetDisplayedHouseholdFinance();

            if (finance is null)
                return string.Empty;

            return FormatFinanceBreakdown(
                finance.LastExpenseBreakdown,
                "No expenses were recorded in the last annual finance pass.");
        }
    }

    public string HouseholdHousesText
    {
        get
        {
            var finance =
                GetDisplayedHouseholdFinance();

            return finance is null
                ? string.Empty
                : $"Houses: {finance.Houses.Count}";
        }
    }

    public string HouseholdHousesDetailsText
    {
        get
        {
            var head =
                _succession.ActiveController;

            var finance =
                GetDisplayedHouseholdFinance();

            if (head is null
                || finance is null)
            {
                return string.Empty;
            }

            if (finance.Houses.Count == 0)
            {
                var homeTown =
                    _locationService?
                        .GetLocation(
                            head)
                        .HomeTown
                        .Town;

                return string.IsNullOrWhiteSpace(
                    homeTown)
                        ? "No owned houses. The household rents its residence."
                        : $"{homeTown} — Renting";
            }

            return string.Join(
                Environment.NewLine,
                finance.Houses.Select(
                    house =>
                        $"{house.Town.Town} — " +
                        $"{house.Status}"));
        }
    }

    // Retained for compatibility with older bindings/packages.
    public string HouseholdIncomeExpensesText =>
        string.Join(
            " | ",
            new[]
            {
                HouseholdIncomeText,
                HouseholdExpensesText
            }
            .Where(
                value =>
                    !string.IsNullOrWhiteSpace(
                        value)));

    private HouseholdFinanceSnapshot?
        GetDisplayedHouseholdFinance()
    {
        var head =
            GetDisplayedHouseholdHead();

        return head is null
            ? null
            : _economyService?.GetHousehold(
                head);
    }

    private static string FormatFinanceBreakdown(
        IReadOnlyList<FinanceBreakdownItem> items,
        string emptyText)
    {
        if (items.Count == 0)
            return emptyText;

        return string.Join(
            Environment.NewLine,
            items.Select(
                item =>
                    $"{item.Amount:N0} zł — " +
                    $"{item.Label}"));
    }

    public string HouseholdWarningText
    {
        get
        {
            var head =
                GetDisplayedHouseholdHead();

            var status =
                head is null
                    ? null
                    : _householdService?.GetStatus(
                        head);

            return status is null
                ? string.Empty
                : string.Join(
                    Environment.NewLine,
                    status.Warnings);
        }
    }

    public string ExtendedFamilyEmptyText =>
        ExtendedFamilyMembers.Count == 0
            ? "No extended family members in this household."
            : string.Empty;

    public string DeceasedFamilyEmptyText =>
        DeceasedFamilyMembers.Count == 0
            ? "No deceased family members."
            : string.Empty;

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

    public string ActionsEmptyText
    {
        get
        {
            if (IsBloodlineFamilyView)
            {
                return GetDisplayedHouseholdHead()
                    is null
                        ? "No autonomous bloodline households."
                        : "This household lives independently.";
            }

            if (AvailableActions.Count > 0
                || HasQueuedAction)
            {
                return string.Empty;
            }

            var actor =
                _succession.ActiveController;

            if (actor is not null)
            {
                var blockedReason =
                    _actionRegistry.GetBlockedReason(
                        actor);

                if (!string.IsNullOrWhiteSpace(
                    blockedReason))
                {
                    return blockedReason;
                }
            }

            return
                "No actions available for the selected person.";
        }
    }

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

    public ObservableCollection<PlayablePersonTabViewModel>
        PlayableTabs { get; } = [];

    public ObservableCollection<PlayablePersonTabViewModel>
        BloodlineTabs { get; } = [];

    public ObservableCollection<FamilyMemberCardViewModel>
        HouseholdMembers { get; } = [];

    public ObservableCollection<FamilyMemberCardViewModel>
        ExtendedFamilyMembers { get; } = [];

    public ObservableCollection<FamilyMemberCardViewModel>
        DeceasedFamilyMembers { get; } = [];

    public ObservableCollection<StatValue>
        SelectedStats { get; } = [];

    public ObservableCollection<BiographyEntryViewModel>
        SelectedBiography { get; } = [];

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
            RefreshEducation();
            RefreshCareer();
            RefreshMarriageSatisfaction();
            RefreshJustice();
            RefreshNarrative();
            RefreshActions();
            RefreshFamilySection();

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(SelectedPersonEmoji));
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

    public EducationViewModel? SelectedEducation
    {
        get => _selectedEducation;
        private set
        {
            _selectedEducation = value;
            OnPropertyChanged();
        }
    }

    public CareerViewModel? SelectedCareer
    {
        get => _selectedCareer;
        private set
        {
            _selectedCareer = value;
            OnPropertyChanged();
        }
    }

    public string SelectedMarriageSatisfactionText
    {
        get
        {
            var person =
                FindSelectedPerson();

            var snapshot =
                person is null
                    ? null
                    : _marriageSatisfactionService?
                        .GetSatisfaction(
                            person);

            return snapshot is null
                ? string.Empty
                : $"Marriage satisfaction: " +
                  $"{snapshot.Label}";
        }
    }

    public string SelectedMarriageSatisfactionDetailsText
    {
        get
        {
            var person =
                FindSelectedPerson();

            var snapshot =
                person is null
                    ? null
                    : _marriageSatisfactionService?
                        .GetSatisfaction(
                            person);

            if (snapshot is null)
                return string.Empty;

            return snapshot.CurrentIssues.Count == 0
                ? "No current marriage strains."
                : "Current strains:" +
                  Environment.NewLine +
                  string.Join(
                      Environment.NewLine,
                      snapshot.CurrentIssues.Select(
                          issue =>
                              $"• {issue}"));
        }
    }

    public bool HasSelectedMarriageSatisfaction =>
        !string.IsNullOrWhiteSpace(
            SelectedMarriageSatisfactionText);

    public JusticeViewModel? SelectedJustice
    {
        get => _selectedJustice;

        private set
        {
            _selectedJustice = value;
            OnPropertyChanged();
        }
    }

    public bool HasSelectedEconomy =>
        SelectedEconomy is not null;

    public string SelectedAboutText
    {
        get => _selectedAboutText;

        private set
        {
            if (_selectedAboutText == value)
                return;

            _selectedAboutText = value;
            OnPropertyChanged();
        }
    }

    public string BiographyEmptyText =>
        SelectedBiography.Count == 0
            ? "No significant events recorded."
            : string.Empty;

    public int DetailsTabIndex
    {
        get => _detailsTabIndex;

        set
        {
            var clamped =
                Math.Clamp(
                    value,
                    0,
                    3);

            if (_detailsTabIndex == clamped)
                return;

            _detailsTabIndex =
                clamped;

            OnPropertyChanged();
        }
    }

    public string PersistenceStatusText
    {
        get => _persistenceStatusText;

        private set
        {
            if (_persistenceStatusText == value)
                return;

            _persistenceStatusText = value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasPersistenceStatus));
        }
    }

    public bool HasPersistenceStatus =>
        !string.IsNullOrWhiteSpace(
            PersistenceStatusText);

    public RelayCommand StartGameCommand { get; }
    public RelayCommand NextYearCommand { get; }
    public RelayCommand CancelQueuedActionCommand { get; }
    public RelayCommand GoBackFromGameOverCommand { get; }
    public RelayCommand ReturnToMainMenuCommand { get; }
    public RelayCommand ShowMainMenuPromptCommand { get; }
    public RelayCommand HideMainMenuPromptCommand { get; }
    public RelayCommand ShowLivingFamilyCommand { get; }
    public RelayCommand ShowBloodlineFamilyCommand { get; }
    public RelayCommand ShowDeceasedFamilyCommand { get; }
    public RelayCommand PreviousAlbumYearCommand { get; }
    public RelayCommand NextAlbumYearCommand { get; }

    public string GetSuggestedSaveFileName()
    {
        return _saveService
            .GetSuggestedFileName();
    }

    public void SaveGame(
        Stream stream)
    {
        if (!IsGameStarted)
        {
            throw new InvalidOperationException(
                "There is no active dynasty to save.");
        }

        _saveService.Save(
            stream,
            CaptureUiSaveState());
    }

    public void LoadGame(
        Stream stream)
    {
        var loadedUiState =
            _saveService.Load(
                stream,
                CaptureUiSaveState());

        SurnameInput =
            _gameState.DynastySurname;

        IsLivingFamilyView =
            loadedUiState.IsLivingFamilyView;

        DetailsTabIndex =
            loadedUiState.DetailsTabIndex;

        IsMainMenuPromptVisible =
            false;

        IsGameStarted =
            true;

        AlbumYear =
            Math.Clamp(
                loadedUiState.AlbumYear,
                1900,
                _gameState.Year);

        IsGameOverOverlayVisible =
            _succession.IsGameOver;

        _personalityService?
            .ReconcileAll();

        _householdService?
            .ReconcileHouseholds();

        _thoughtService?
            .ResetAfterLoad();

        RefreshPeople();
        RefreshAlbum();

        NotifyGameStateChanged();
    }

    public void ReportPersistenceStatus(
        string message)
    {
        _loadedStatusCancellation?
            .Cancel();

        _loadedStatusCancellation?
            .Dispose();

        _loadedStatusCancellation =
            null;

        PersistenceStatusText =
            message;

        if (!ShouldAutoClearPersistenceStatus(
                message))
        {
            return;
        }

        var cancellation =
            new CancellationTokenSource();

        _loadedStatusCancellation =
            cancellation;

        _ =
            ClearLoadedStatusAfterDelayAsync(
                message,
                cancellation);
    }

    private bool ShouldAutoClearPersistenceStatus(
        string message)
    {
        return
            message.StartsWith(
                "Loaded ",
                StringComparison.OrdinalIgnoreCase)
            || message.StartsWith(
                "Choose an annual action",
                StringComparison.OrdinalIgnoreCase);
    }

    private async Task ClearLoadedStatusAfterDelayAsync(
        string expectedMessage,
        CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(15),
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                if (ReferenceEquals(
                        _loadedStatusCancellation,
                        cancellation)
                    && PersistenceStatusText.Equals(
                        expectedMessage,
                        StringComparison.Ordinal))
                {
                    PersistenceStatusText =
                        string.Empty;

                    _loadedStatusCancellation?
                        .Dispose();

                    _loadedStatusCancellation =
                        null;
                }
            });
    }

    private GameUiSaveState CaptureUiSaveState()
    {
        return new GameUiSaveState(
            _selectionService.SelectedPersonId,
            _succession.ActiveControllerId,
            AlbumYear,
            IsLivingFamilyView,
            DetailsTabIndex);
    }

    private void StartGame()
    {
        PersistenceStatusText =
            string.Empty;

        _newGameService.StartNewGame(
            SurnameInput);

        _succession.Refresh();

        _thoughtService?
            .EnsureCurrentThoughts();

        IsLivingFamilyView = true;

        IsGameOverOverlayVisible =
            false;

        IsMainMenuPromptVisible =
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

        var missing =
            GetMaleHeirsWithoutAnnualAction();

        if (missing.Count > 0)
        {
            var first =
                missing[0];

            _succession.SetActiveController(
                first);

            var row =
                People.FirstOrDefault(
                    person =>
                        person.Id
                        == first.Id);

            if (row is not null)
            {
                SelectedPerson =
                    row;
            }

            ReportPersistenceStatus(
                "Choose an annual action for every adult male heir before advancing the year.");

            RefreshFamilySection();
            RefreshActions();

            return;
        }

        PersistenceStatusText =
            string.Empty;

        _yearProcessor.AdvanceYear();

        AlbumYear =
            _gameState.Year;

        RefreshPeople();

        NotifyGameStateChanged();
    }

    private IReadOnlyList<IPerson>
        GetMaleHeirsWithoutAnnualAction()
    {
        var queuedActorIds =
            _actionRegistry
                .GetAllQueuedActions()
                .Select(
                    queued =>
                        queued.ActorId)
                .ToHashSet();

        return _gameState.People
            .Where(
                person =>
                    _succession.IsControllable(
                        person)
                    && !queuedActorIds.Contains(
                        person.Id))
            .OrderBy(
                person =>
                    _familyService?
                        .GetGeneration(
                            person)
                    ?? int.MaxValue)
            .ThenBy(
                GetBirthSortYear)
            .ThenBy(
                person =>
                    person.Id)
            .ToList();
    }

    private void CancelQueuedAction()
    {
        var actor =
            _succession.ActiveController;

        if (actor is null)
            return;

        _actionRegistry
            .CancelQueuedActions(
                actor);

        RefreshActions();
        RefreshFamilySection();
    }

    private void HideGameOver()
    {
        IsGameOverOverlayVisible =
            false;
    }

    private void ShowMainMenuPrompt()
    {
        if (!IsGameStarted)
        {
            return;
        }

        IsMainMenuPromptVisible =
            true;
    }

    private void HideMainMenuPrompt()
    {
        IsMainMenuPromptVisible =
            false;
    }

    private void ReturnToMainMenu()
    {
        IsGameOverOverlayVisible =
            false;

        IsMainMenuPromptVisible =
            false;

        IsGameStarted =
            false;

        PersistenceStatusText =
            string.Empty;

        OnPropertyChanged(
            nameof(DynastyTitle));
    }

    private void ShowLivingFamily()
    {
        SetHouseholdViewMode(
            HouseholdViewMode.Lineage);

        var active =
            _succession.ActiveController;

        if (active is not null)
        {
            var row =
                People.FirstOrDefault(
                    person =>
                        person.Id
                        == active.Id);

            if (row is not null)
            {
                SelectedPerson =
                    row;
            }
        }

        RefreshFamilySection();
        RefreshActions();
    }

    private void ShowBloodlineFamily()
    {
        SetHouseholdViewMode(
            HouseholdViewMode.Bloodline);

        var first =
            _householdService?
                .GetActiveHouseholds()
                .FirstOrDefault(
                    household =>
                        household.Class
                        == HouseholdClass.Bloodline);

        _inspectedBloodlineHouseholdId ??=
            first?.HouseholdId;

        if (_inspectedBloodlineHouseholdId
            is Guid inspectedId)
        {
            var inspected =
                _householdService?
                    .GetActiveHouseholds()
                    .FirstOrDefault(
                        household =>
                            household.HouseholdId
                                == inspectedId
                            && household.Class
                                == HouseholdClass.Bloodline);

            var row =
                inspected is null
                    ? null
                    : People.FirstOrDefault(
                        person =>
                            person.Id
                            == inspected.HeadId);

            if (row is not null)
            {
                SelectedPerson =
                    row;
            }
        }

        RefreshFamilySection();
        RefreshActions();
    }

    private void ShowDeceasedFamily()
    {
        SetHouseholdViewMode(
            HouseholdViewMode.Deceased);

        RefreshFamilySection();
        RefreshActions();
    }

    private void SetHouseholdViewMode(
        HouseholdViewMode mode)
    {
        if (_householdViewMode
            == mode)
        {
            return;
        }

        _householdViewMode =
            mode;

        OnPropertyChanged(
            nameof(IsLivingFamilyView));

        OnPropertyChanged(
            nameof(IsLineageFamilyView));

        OnPropertyChanged(
            nameof(IsBloodlineFamilyView));

        OnPropertyChanged(
            nameof(IsDeceasedFamilyView));

        OnPropertyChanged(
            nameof(ActionsEmptyText));
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
                    _economyService,
                    _careerService,
                    _householdService,
                    _locationService));
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
        RefreshEducation();
        RefreshCareer();
        RefreshJustice();
        RefreshNarrative();
        RefreshActions();
        RefreshFamilySection();
    }

    private void RefreshFamilySection()
    {
        PlayableTabs.Clear();
        BloodlineTabs.Clear();
        HouseholdMembers.Clear();
        ExtendedFamilyMembers.Clear();
        DeceasedFamilyMembers.Clear();

        var selectedId =
            SelectedPerson?.Id;

        var households =
            _householdService?
                .GetActiveHouseholds()
            ?? Array.Empty<HouseholdInfo>();

        var active =
            _succession.ActiveController;

        foreach (var household in
            households
                .Where(
                    household =>
                        household.Class
                        == HouseholdClass.Lineage)
                .OrderBy(
                    household =>
                        household.Generation
                        ?? int.MaxValue)
                .ThenBy(
                    GetHouseholdHeadBirthSortKey)
                .ThenBy(
                    household =>
                        household.HeadId))
        {
            var head =
                _gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id
                            == household.HeadId);

            if (head is null)
                continue;

            var queuedActions =
                _actionRegistry
                    .GetQueuedActions(
                        head);

            var summary =
                $"{household.HeadName}" +
                Environment.NewLine +
                BuildHouseholdActionSummary(
                    queuedActions.FirstOrDefault());

            PlayableTabs.Add(
                new PlayablePersonTabViewModel(
                    head.Id,
                    household.Generation is int generation
                        ? $"G{generation}"
                        : string.Empty,
                    $"{head.Name} ({head.Age})",
                    household.HeadName,
                    summary,
                    active?.Id
                        == head.Id,
                    queuedActions.Count > 0,
                    SwitchActiveHousehold));
        }

        foreach (var household in
            households
                .Where(
                    household =>
                        household.Class
                        == HouseholdClass.Bloodline)
                .OrderBy(
                    household =>
                        household.Generation
                        ?? int.MaxValue)
                .ThenBy(
                    household =>
                        household.Surname,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    GetHouseholdHeadBirthSortKey)
                .ThenBy(
                    household =>
                        household.HouseholdId))
        {
            var head =
                _gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id
                            == household.HeadId);

            if (head is null)
                continue;

            BloodlineTabs.Add(
                new PlayablePersonTabViewModel(
                    head.Id,
                    household.Generation is int generation
                        ? $"G{generation}"
                        : string.Empty,
                    household.Surname,
                    household.HeadName,
                    $"{household.HeadName}" +
                    Environment.NewLine +
                    "This household lives independently.",
                    _inspectedBloodlineHouseholdId
                        == household.HouseholdId,
                    false,
                    InspectBloodlineHousehold));
        }

        if (IsBloodlineFamilyView)
        {
            var availableIds =
                households
                    .Where(
                        household =>
                            household.Class
                            == HouseholdClass.Bloodline)
                    .Select(
                        household =>
                            household.HouseholdId)
                    .ToHashSet();

            if (_inspectedBloodlineHouseholdId is null
                || !availableIds.Contains(
                    _inspectedBloodlineHouseholdId.Value))
            {
                _inspectedBloodlineHouseholdId =
                    availableIds
                        .Cast<Guid?>()
                        .FirstOrDefault();
            }
        }

        var displayed =
            GetDisplayedHouseholdHead(
                households);

        if (IsLivingFamilyView
            && displayed is not null)
        {
            var info =
                households.FirstOrDefault(
                    household =>
                        household.HeadId
                        == displayed.Id);

            if (info is not null)
            {
                foreach (var member in
                    info.MemberIds
                        .Select(
                            id =>
                                _gameState.People
                                    .FirstOrDefault(
                                        person =>
                                            person.Id
                                            == id))
                        .Where(
                            person =>
                                person is not null
                                && person.Tags.Has(
                                    "state.alive")
                                && !person.Tags.Has(
                                    "role.nanny"))
                        .Cast<IPerson>()
                        .OrderByDescending(
                            person =>
                                person.Id
                                == displayed.Id)
                        .ThenBy(
                            GetBirthSortYear)
                        .ThenBy(
                            person =>
                                person.Id))
                {
                    AddHouseholdCard(
                        member,
                        selectedId,
                        displayed.Id);
                }
            }
        }

        foreach (var person in
            _gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.dead")
                        && !person.Tags.Has(
                            "role.nanny")
                        && IsDynastyRelevantDeceased(
                            person))
                .OrderByDescending(
                    person =>
                        person.DeathDate?.Year
                        ?? int.MinValue)
                .ThenByDescending(
                    person =>
                        person.DeathDate?.Month
                        ?? 1)
                .ThenByDescending(
                    person =>
                        person.DeathDate?.Day
                        ?? 1)
                .ThenByDescending(
                    GetBirthSortYear))
        {
            DeceasedFamilyMembers.Add(
                CreateFamilyCard(
                    person,
                    selectedId,
                    null));
        }

        OnPropertyChanged(
            nameof(HasActiveHousehold));

        OnPropertyChanged(
            nameof(HouseholdBudgetText));

        OnPropertyChanged(
            nameof(HouseholdHousesText));

        OnPropertyChanged(
            nameof(HouseholdHousesDetailsText));

        OnPropertyChanged(
            nameof(HouseholdIncomeText));

        OnPropertyChanged(
            nameof(HouseholdIncomeDetailsText));

        OnPropertyChanged(
            nameof(HouseholdExpensesText));

        OnPropertyChanged(
            nameof(HouseholdExpenseDetailsText));

        OnPropertyChanged(
            nameof(HouseholdIncomeExpensesText));

        OnPropertyChanged(
            nameof(HouseholdWarningText));

        OnPropertyChanged(
            nameof(ExtendedFamilyEmptyText));

        OnPropertyChanged(
            nameof(DeceasedFamilyEmptyText));

        OnPropertyChanged(
            nameof(ActiveHouseholdText));

        OnPropertyChanged(
            nameof(ActionsEmptyText));
    }

    private bool IsDynastyRelevantDeceased(
        IPerson person)
    {
        if (_familyService is null)
            return true;

        if (_familyService.IsBloodline(
            person))
        {
            return true;
        }

        return _familyService
            .GetRelationshipHistory(
                person)
            .Any(
                relationship =>
                {
                    var spouse =
                        _gameState.People
                            .FirstOrDefault(
                                candidate =>
                                    candidate.Id
                                    == relationship.SpouseId);

                    return spouse is not null
                        && _familyService.IsBloodline(
                            spouse);
                });
    }

    private long GetHouseholdHeadBirthSortKey(
        HouseholdInfo household)
    {
        var head =
            _gameState.People
                .FirstOrDefault(
                    person =>
                        person.Id
                        == household.HeadId);

        if (head is null)
            return long.MaxValue;

        var year =
            head.BirthDate?.Year
            ?? GetBirthSortYear(
                head);

        var month =
            head.BirthDate?.Month
            ?? 1;

        var day =
            head.BirthDate?.Day
            ?? 1;

        return year * 10000L
            + month * 100L
            + day;
    }

    private IPerson? GetDisplayedHouseholdHead()
    {
        return GetDisplayedHouseholdHead(
            _householdService?
                .GetActiveHouseholds()
            ?? Array.Empty<HouseholdInfo>());
    }

    private IPerson? GetDisplayedHouseholdHead(
        IReadOnlyList<HouseholdInfo> households)
    {
        if (IsBloodlineFamilyView)
        {
            if (_inspectedBloodlineHouseholdId
                is not Guid inspectedId)
            {
                return null;
            }

            var household =
                households.FirstOrDefault(
                    candidate =>
                        candidate.HouseholdId
                            == inspectedId
                        && candidate.Class
                            == HouseholdClass.Bloodline);

            if (household is null)
                return null;

            return _gameState.People
                .FirstOrDefault(
                    person =>
                        person.Id
                        == household.HeadId);
        }

        if (IsDeceasedFamilyView)
            return null;

        return _succession.ActiveController;
    }

    private void InspectBloodlineHousehold(
        Guid personId)
    {
        var household =
            _householdService?
                .GetActiveHouseholds()
                .FirstOrDefault(
                    candidate =>
                        candidate.Class
                            == HouseholdClass.Bloodline
                        && candidate.HeadId
                            == personId);

        _inspectedBloodlineHouseholdId =
            household?.HouseholdId;

        SetHouseholdViewMode(
            HouseholdViewMode.Bloodline);

        var row =
            People.FirstOrDefault(
                candidate =>
                    candidate.Id
                    == personId);

        if (row is not null)
        {
            SelectedPerson =
                row;
        }

        RefreshFamilySection();
        RefreshActions();
    }

    private void AddHouseholdCard(
        IPerson person,
        Guid? selectedId,
        Guid activeHeadId)
    {
        if (person.Tags.Has(
            "role.nanny"))
        {
            return;
        }

        HouseholdMembers.Add(
            CreateFamilyCard(
                person,
                selectedId,
                activeHeadId));
    }

    private FamilyMemberCardViewModel
        CreateFamilyCard(
            IPerson person,
            Guid? selectedId,
            Guid? activeHeadId)
    {
        return new FamilyMemberCardViewModel(
            person,
            _familyService,
            _healthService,
            _educationService,
            _careerService,
            _justiceService,
            _statsService,
            _locationService,
            _marriageSatisfactionService,
            _thoughtService,
            selectedId == person.Id,
            activeHeadId == person.Id,
            SelectFamilyMember);
    }

    private string BuildHouseholdActionSummary(
        QueuedActionInfo? queued)
    {
        if (queued is null)
        {
            return
                "No action selected for this household.";
        }

        if (queued.ActionId.Equals(
            "turn.pass",
            StringComparison.OrdinalIgnoreCase))
        {
            return
                $"{ActionEmojiMap.Format(queued.ActionId, queued.Label)}" +
                Environment.NewLine +
                "No planned action for this year.";
        }

        var target =
            _gameState.People
                .FirstOrDefault(
                    person =>
                        person.Id
                        == queued.TargetId);

        var targetName =
            target is null
                ? "Unknown"
                : _familyService is null
                    ? $"{target.Name} {target.Surname}"
                    : _familyService.GetDisplayName(
                        target);

        var description =
            string.IsNullOrWhiteSpace(
                queued.Description)
                ? "This action is queued for the next annual pass."
                : queued.Description;

        return
            $"{ActionEmojiMap.Format(queued.ActionId, queued.Label)}" +
            Environment.NewLine +
            $"Target: {targetName}" +
            Environment.NewLine +
            description;
    }

    private int GetBirthSortYear(
        IPerson person)
    {
        return person.BirthDate?.Year
            ?? _gameState.Year
               - person.Age;
    }

    private void SelectFamilyMember(
        Guid personId)
    {
        var row =
            People.FirstOrDefault(
                person =>
                    person.Id == personId);

        if (row is not null)
            SelectedPerson = row;
    }

    private void SwitchActiveHousehold(
        Guid personId)
    {
        var person =
            _gameState.People
                .FirstOrDefault(
                    candidate =>
                        candidate.Id == personId);

        if (person is null
            || !_succession.SetActiveController(
                person))
        {
            return;
        }

        var row =
            People.FirstOrDefault(
                candidate =>
                    candidate.Id == personId);

        if (row is not null)
            SelectedPerson = row;

        SetHouseholdViewMode(
            HouseholdViewMode.Lineage);

        RefreshFamilySection();
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

        var status =
            snapshot is null
                ? null
                : _householdService?.GetStatus(
                    person);

        SelectedEconomy =
            snapshot is null
                ? null
                : new EconomyViewModel(
                    snapshot,
                    status);
    }

    private void RefreshEducation()
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _educationService is null)
        {
            SelectedEducation = null;
            return;
        }

        SelectedEducation =
            new EducationViewModel(
                person.Age,
                _educationService
                    .GetEducationLevel(person));
    }

    private void RefreshCareer()
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _careerService is null)
        {
            SelectedCareer = null;
            return;
        }

        SelectedCareer =
            new CareerViewModel(
                _careerService.GetCareer(person),
                person.Tags.Has("state.alive"));
    }

    private void RefreshMarriageSatisfaction()
    {
        OnPropertyChanged(
            nameof(
                SelectedMarriageSatisfactionText));

        OnPropertyChanged(
            nameof(
                SelectedMarriageSatisfactionDetailsText));

        OnPropertyChanged(
            nameof(
                HasSelectedMarriageSatisfaction));
    }

    private void RefreshJustice()
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _justiceService is null)
        {
            SelectedJustice = null;
            return;
        }

        SelectedJustice =
            new JusticeViewModel(
                _justiceService.GetStatus(
                    person));
    }

    private void RefreshNarrative()
    {
        SelectedBiography.Clear();

        var person =
            FindSelectedPerson();

        if (person is null
            || _biographyService is null)
        {
            SelectedAboutText =
                "No information available.";

            OnPropertyChanged(
                nameof(BiographyEmptyText));

            return;
        }

        SelectedAboutText =
            _biographyService.GetAbout(
                person);

        foreach (var entry in
            _biographyService.GetBiography(
                person))
        {
            SelectedBiography.Add(
                new BiographyEntryViewModel(
                    entry));
        }

        OnPropertyChanged(
            nameof(BiographyEmptyText));
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
            GetSimulatedSiblings(
                person,
                father);

        var relationshipHistory =
            _familyService
                .GetRelationshipHistory(
                    person);

        var hasUnknownFoundingBackground =
            _familyService.GetGeneration(
                person) == 0
            && _familyService.IsBloodline(
                person)
            && generatedBackground is null;

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
                    father is not null
                        ? PersonNameWithLifeYears(
                            father)
                        : generatedBackground?.FatherName
                          ?? (hasUnknownFoundingBackground
                              ? "Unknown"
                              : "None"),

                Mother =
                    mother is not null
                        ? PersonNameWithLifeYears(
                            mother)
                        : generatedBackground?.MotherName
                          ?? (hasUnknownFoundingBackground
                              ? "Unknown"
                              : "None"),

                Siblings =
                    siblings.Count > 0
                        ? FormatPeopleWithLifeYears(
                            siblings)
                        : generatedBackground is not null
                            ? FormatNames(
                                generatedBackground.Siblings)
                            : hasUnknownFoundingBackground
                                ? "Unknown"
                                : "None",

                Spouse =
                    PersonNameWithLifeYears(
                        spouse),

                Children =
                    FormatPeopleWithLifeYears(
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

        var actor =
            _succession.ActiveController;

        var target =
            FindSelectedPerson();

        HasQueuedAction = false;
        QueuedActionText =
            string.Empty;

        if (IsBloodlineFamilyView)
        {
            OnPropertyChanged(
                nameof(ActionsEmptyText));

            return;
        }

        if (actor is not null
            && target is not null
            && !_succession.IsGameOver)
        {
            var queued =
                _actionRegistry
                    .GetQueuedActions(
                        actor);

            if (queued.Count > 0)
            {
                HasQueuedAction =
                    true;

                QueuedActionText =
                    $"Queued: " +
                    ActionEmojiMap.Format(
                        queued[0].ActionId,
                        queued[0].Label);
            }
            else
            {
                foreach (var action in
                    OrderAvailableActions(
                        _actionRegistry
                            .GetAvailableActions(
                                actor,
                                target)))
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

    private static IReadOnlyList<GameActionDefinition>
        OrderAvailableActions(
            IReadOnlyList<GameActionDefinition> source)
    {
        var actions =
            source.ToList();

        var connections =
            actions.FirstOrDefault(
                action =>
                    action.Id.Equals(
                        "career.use_family_connections",
                        StringComparison.OrdinalIgnoreCase));

        if (connections is not null)
        {
            actions.Remove(
                connections);

            var seekIndex =
                actions.FindIndex(
                    action =>
                        action.Id.Equals(
                            "career.seek_employment",
                            StringComparison.OrdinalIgnoreCase)
                        || action.Id.Equals(
                            "career.help_seek_employment",
                            StringComparison.OrdinalIgnoreCase));

            if (seekIndex >= 0)
            {
                actions.Insert(
                    seekIndex + 1,
                    connections);
            }
            else
            {
                actions.Add(
                    connections);
            }
        }

        var pass =
            actions.FirstOrDefault(
                action =>
                    action.Id.Equals(
                        "turn.pass",
                        StringComparison.OrdinalIgnoreCase));

        if (pass is not null)
        {
            actions.Remove(
                pass);

            actions.Add(
                pass);
        }

        return actions;
    }

    private void ExecuteAction(
        string actionId)
    {
        var actor =
            _succession.ActiveController;

        var target =
            FindSelectedPerson();

        if (actor is null
            || target is null
            || _succession.IsGameOver)
        {
            return;
        }

        var result =
            _actionRegistry.Execute(
                actionId,
                actor,
                target);

        if (result.Success
            && GetMaleHeirsWithoutAnnualAction()
                .Count == 0
            && PersistenceStatusText.StartsWith(
                "Choose ",
                StringComparison.OrdinalIgnoreCase))
        {
            PersistenceStatusText =
                string.Empty;
        }

        RefreshPeople();
        RefreshAlbum();
        RefreshHealth();
        RefreshEconomy();
        RefreshEducation();
        RefreshCareer();
        RefreshJustice();
        RefreshNarrative();
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
                AlbumYear)
                .Where(
                    gameEvent =>
                        _householdService?
                            .ShouldShowFamilyNews(
                                gameEvent)
                        ?? true))
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

        RefreshNarrative();
    }

    private void OnSelectionChanged(
        object? sender,
        EventArgs e)
    {
        var selectedId =
            _selectionService
                .SelectedPersonId;

        var row =
            selectedId is Guid id
                ? People.FirstOrDefault(
                    person =>
                        person.Id == id)
                : null;

        if (ReferenceEquals(
            row,
            SelectedPerson))
        {
            return;
        }

        SelectedPerson =
            row;
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

        OnPropertyChanged(
            nameof(ActiveHouseholdText));

        OnPropertyChanged(
            nameof(SelectedPersonEmoji));

        RefreshFamilySection();
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

        OnPropertyChanged(
            nameof(ActiveHouseholdText));

        OnPropertyChanged(
            nameof(SelectedPersonEmoji));

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

    private string PersonNameWithLifeYears(
        IPerson? person)
    {
        if (person is null)
            return "None";

        var name =
            PersonName(
                person);

        var birthYear =
            person.BirthDate?.Year;

        var deathYear =
            person.DeathDate?.Year;

        if (birthYear is int born
            && deathYear is int died)
        {
            return $"{name} ({born}–{died})";
        }

        if (birthYear is int livingBorn)
        {
            return person.Tags.Has(
                "state.dead")
                    ? $"{name} ({livingBorn}–?)"
                    : $"{name} ({livingBorn}–)";
        }

        if (deathYear is int knownDeath)
        {
            return $"{name} (?–{knownDeath})";
        }

        return name;
    }

    private string FormatPeopleWithLifeYears(
        IReadOnlyList<IPerson> people)
    {
        return people.Count == 0
            ? "None"
            : string.Join(
                ", ",
                people.Select(
                    PersonNameWithLifeYears));
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

    private enum HouseholdViewMode
    {
        Lineage = 0,
        Bloodline = 1,
        Deceased = 2
    }

}
