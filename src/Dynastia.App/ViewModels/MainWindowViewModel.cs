using Dynastia.App.Persistence;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IGameState _gameState;
    private readonly INewGameService _newGameService;
    private readonly YearProcessor _yearProcessor;
    private readonly ISelectionService _selectionService;
    private readonly IStatsService? _statsService;
    private readonly IFamilyService? _familyService;
    private readonly INationalityService? _nationalityService;
    private readonly IHealthService? _healthService;
    private readonly IStressService? _stressService;
    private readonly IEconomyService? _economyService;
    private readonly IFarmingService? _farmingService;
    private readonly ICraftService? _craftService;
    private readonly ILoanService? _loanService;
    private readonly IHouseholdService? _householdService;
    private readonly IAutonomousHouseholdDecisionService?
        _autonomousHouseholdDecisionService;
    private readonly IAdoptionService? _adoptionService;
    private readonly ILocationService? _locationService;
    private readonly ILocalCareerOpportunityService? _localCareerOpportunityService;
    private readonly IMarriageSatisfactionService?
        _marriageSatisfactionService;
    private readonly IFamilyRelationService?
        _familyRelationService;
    private readonly IThoughtService? _thoughtService;
    private readonly IHobbyService? _hobbyService;
    private readonly IPersonalityService? _personalityService;
    private readonly IAppearanceService? _appearanceService;
    private readonly IChildHappinessService? _childHappinessService;
    private readonly IEducationService? _educationService;
    private readonly ICareerService? _careerService;
    private readonly ICareerPresentationService?
        _careerPresentationService;
    private readonly IPartnerSearchService? _partnerSearchService;
    private readonly IJusticeService? _justiceService;
    private readonly IBiographyService? _biographyService;
    private readonly ISuccessionService _succession;
    private readonly IGameEventBus _eventBus;
    private readonly IActionRegistry _actionRegistry;
    private readonly GameSaveService _saveService;
    private readonly IStateReconciliationLifecycle _reconciliation;

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
    private double _selectedStartYear =
        GameCalendarConfiguration.GameStartYear;
    private bool _isGameStarted;
    private string _queuedActionText = string.Empty;
    private bool _hasQueuedAction;
    private bool _isGameOverOverlayVisible;
    private bool _isMainMenuPromptVisible;
    private bool _isYearSummaryVisible;
    private int _yearSummaryEventYear =
        GameCalendarConfiguration.GameStartYear;
    private HouseholdViewMode _householdViewMode =
        HouseholdViewMode.Lineage;

    private Guid? _inspectedBloodlineHouseholdId;

    private int _detailsTabIndex;
    private string _persistenceStatusText = string.Empty;

    private readonly List<AvailableActionViewModel>
        _allAvailableActions = [];

    private readonly HashSet<ActionCategory>
        _activeActionCategories =
            new(Enum.GetValues<ActionCategory>());

    public MainWindowViewModel(
        IGameState gameState,
        INewGameService newGameService,
        YearProcessor yearProcessor,
        ISelectionService selectionService,
        IStatsService? statsService,
        IFamilyService? familyService,
        INationalityService? nationalityService,
        IHealthService? healthService,
        IStressService? stressService,
        IEconomyService? economyService,
        IFarmingService? farmingService,
        ICraftService? craftService,
        ILoanService? loanService,
        IHouseholdService? householdService,
        IAutonomousHouseholdDecisionService? autonomousHouseholdDecisionService,
        IAdoptionService? adoptionService,
        ILocationService? locationService,
        ILocalCareerOpportunityService? localCareerOpportunityService,
        IMarriageSatisfactionService? marriageSatisfactionService,
        IFamilyRelationService? familyRelationService,
        IThoughtService? thoughtService,
        IHobbyService? hobbyService,
        IPersonalityService? personalityService,
        IAppearanceService? appearanceService,
        IChildHappinessService? childHappinessService,
        IEducationService? educationService,
        ICareerService? careerService,
        ICareerPresentationService? careerPresentationService,
        IPartnerSearchService? partnerSearchService,
        IJusticeService? justiceService,
        IBiographyService? biographyService,
        ISuccessionService succession,
        IGameEventBus eventBus,
        IActionRegistry actionRegistry,
        GameSaveService saveService,
        IStateReconciliationLifecycle reconciliation)
    {
        _gameState = gameState;
        _newGameService = newGameService;
        _yearProcessor = yearProcessor;
        _selectionService = selectionService;
        _statsService = statsService;
        _familyService = familyService;
        _nationalityService = nationalityService;
        _healthService = healthService;
        _stressService = stressService;
        _economyService = economyService;
        _farmingService = farmingService;
        _craftService = craftService;
        _loanService = loanService;
        _householdService = householdService;
        _autonomousHouseholdDecisionService =
            autonomousHouseholdDecisionService;
        _adoptionService = adoptionService;
        _locationService = locationService;
        _localCareerOpportunityService = localCareerOpportunityService;
        _marriageSatisfactionService =
            marriageSatisfactionService;
        _familyRelationService =
            familyRelationService;
        _thoughtService =
            thoughtService;
        _hobbyService =
            hobbyService;
        _personalityService =
            personalityService;
        _appearanceService =
            appearanceService;
        _childHappinessService =
            childHappinessService;
        _educationService = educationService;
        _careerService = careerService;
        _careerPresentationService =
            careerPresentationService;
        _partnerSearchService = partnerSearchService;
        _justiceService = justiceService;
        _biographyService = biographyService;
        _succession = succession;
        _eventBus = eventBus;
        _actionRegistry = actionRegistry;
        _saveService = saveService;
        _reconciliation = reconciliation;

        _albumYear =
            GameCalendarConfiguration.GameStartYear;

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

        HideStatusMessageCommand =
            new RelayCommand(
                () => PersistenceStatusText = string.Empty);

        ShowAlbumYearSummaryCommand =
            new RelayCommand(
                ShowAlbumYearSummary,
                () => IsGameStarted);

        HideYearSummaryCommand =
            new RelayCommand(
                HideYearSummary);

        PreviousYearSummaryCommand =
            new RelayCommand(
                PreviousYearSummary,
                () => IsYearSummaryVisible
                    && _yearSummaryEventYear > _gameState.StartYear + 1);

        NextYearSummaryCommand =
            new RelayCommand(
                NextYearSummary,
                () => IsYearSummaryVisible
                    && _yearSummaryEventYear < Year);

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
                    && AlbumYear >
                        _gameState.StartYear + 1);

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

    public double SelectedStartYear
    {
        get => _selectedStartYear;
        set
        {
            var normalized =
                GameCalendarConfiguration.NormalizeSelectableStartYear(
                    (int)Math.Round(value));

            if (Math.Abs(
                    _selectedStartYear - normalized)
                < 0.001)
            {
                return;
            }

            _selectedStartYear =
                normalized;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(SelectedStartYearText));
        }
    }

    public string SelectedStartYearText =>
        $"Starting year: {(int)SelectedStartYear}";

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
            OnPropertyChanged(
                nameof(HasFamilyRelations));

            NextYearCommand
                .RaiseCanExecuteChanged();

            ShowMainMenuPromptCommand
                .RaiseCanExecuteChanged();

            ShowAlbumYearSummaryCommand
                .RaiseCanExecuteChanged();
        }
    }

    public bool IsStartScreenVisible =>
        !IsGameStarted;

    public string DynastyTitle =>
        IsGameStarted
            ? $"The {_gameState.DynastySurname} Dynasty"
            : "Dynastia";

    public string SelectedPersonPortrait
    {
        get
        {
            var person =
                FindSelectedPerson();

            if (person is null)
                return string.Empty;

            if (_appearanceService is not null)
            {
                return _appearanceService.GetPortrait(
                    person,
                    useDeadOverride: false);
            }

            var sex = _familyService?.GetSex(person)
                ?? (person.Tags.Has("sex.female")
                    ? Sex.Female
                    : Sex.Male);

            if (person.Age <= 4)
                return "👶🏻";

            if (person.Age <= 11)
                return sex == Sex.Male ? "👦🏻" : "👧🏻";

            if (person.Age <= 17)
                return "🧑🏻";

            if (person.Age >= 70)
                return sex == Sex.Male ? "👴🏻" : "👵🏻";

            return sex == Sex.Male ? "👨🏻" : "👩🏻";
        }
    }

    public bool IsSelectedPersonDeceased =>
        FindSelectedPerson()?.Tags.Has("state.dead") == true;

    public int Year =>
        _gameState.Year;

    public string HistoricalEraName =>
        HistoricalEraConfiguration.GetDisplayName(
            _gameState.Year);

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


    public int AlbumYear
    {
        get => _albumYear;
        private set
        {
            if (_albumYear == value)
                return;

            _albumYear = value;
            OnPropertyChanged();
            OnPropertyChanged(
                nameof(AlbumDisplayYear));

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

    public int AlbumDisplayYear =>
        AlbumYear <= _gameState.StartYear
            ? _gameState.StartYear
            : AlbumYear - 1;

    public bool IsYearSummaryVisible
    {
        get => _isYearSummaryVisible;
        private set
        {
            if (_isYearSummaryVisible == value)
                return;

            _isYearSummaryVisible = value;
            OnPropertyChanged();
        }
    }

    public string YearSummaryTitle =>
        $"Year {Math.Max(
            _gameState.StartYear,
            _yearSummaryEventYear - 1)}";

    public string YearSummaryEmptyText =>
        YearSummaryHouseholds.Count == 0
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
                || PassActions.Count > 0
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

    public RelayCommand StartGameCommand { get; }
    public RelayCommand NextYearCommand { get; }
    public RelayCommand CancelQueuedActionCommand { get; }
    public RelayCommand GoBackFromGameOverCommand { get; }
    public RelayCommand ReturnToMainMenuCommand { get; }
    public RelayCommand ShowMainMenuPromptCommand { get; }
    public RelayCommand HideMainMenuPromptCommand { get; }
    public RelayCommand HideStatusMessageCommand { get; }
    public RelayCommand ShowAlbumYearSummaryCommand { get; }
    public RelayCommand HideYearSummaryCommand { get; }
    public RelayCommand PreviousYearSummaryCommand { get; }
    public RelayCommand NextYearSummaryCommand { get; }
    public RelayCommand ShowLivingFamilyCommand { get; }
    public RelayCommand ShowBloodlineFamilyCommand { get; }
    public RelayCommand ShowDeceasedFamilyCommand { get; }
    public RelayCommand PreviousAlbumYearCommand { get; }
    public RelayCommand NextAlbumYearCommand { get; }

}
