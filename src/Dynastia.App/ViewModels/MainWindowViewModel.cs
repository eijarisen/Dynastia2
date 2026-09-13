using System.Collections.ObjectModel;
using Avalonia.Threading;
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
    private readonly IHealthService? _healthService;
    private readonly IEconomyService? _economyService;
    private readonly IHouseholdService? _householdService;
    private readonly IAdoptionService? _adoptionService;
    private readonly ILocationService? _locationService;
    private readonly ILocalCareerOpportunityService? _localCareerOpportunityService;
    private readonly IMarriageSatisfactionService?
        _marriageSatisfactionService;
    private readonly IThoughtService? _thoughtService;
    private readonly IPersonalityService? _personalityService;
    private readonly IChildHappinessService? _childHappinessService;
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
    private bool _isYearSummaryVisible;
    private int _yearSummaryEventYear = 1900;
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
        IHealthService? healthService,
        IEconomyService? economyService,
        IHouseholdService? householdService,
        IAdoptionService? adoptionService,
        ILocationService? locationService,
        ILocalCareerOpportunityService? localCareerOpportunityService,
        IMarriageSatisfactionService? marriageSatisfactionService,
        IThoughtService? thoughtService,
        IPersonalityService? personalityService,
        IChildHappinessService? childHappinessService,
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
        _localCareerOpportunityService = localCareerOpportunityService;
        _marriageSatisfactionService =
            marriageSatisfactionService;
        _thoughtService =
            thoughtService;
        _personalityService =
            personalityService;
        _childHappinessService =
            childHappinessService;
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
                    && AlbumYear > 1901);

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

            if (finance is null)
                return string.Empty;

            var head =
                GetDisplayedHouseholdHead();

            var projected =
                head is null
                    ? finance.LastIncome
                    : _economyService?.GetProjectedAnnualIncome(head)
                        ?? finance.LastIncome;

            return $"Income: {projected:N0} zł";
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

            var head =
                GetDisplayedHouseholdHead();

            var projected =
                head is null
                    ? finance.LastIncomeBreakdown
                    : _economyService?.GetProjectedIncomeBreakdown(head)
                        ?? finance.LastIncomeBreakdown;

            return FormatFinanceBreakdown(
                projected,
                "No current recurring income.");
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

            var lines =
                finance.Houses
                    .Select(
                        house =>
                            $"{house.Town.Town} — " +
                            $"{house.Status}")
                    .ToList();

            if (!finance.Houses.Any(house => house.IsResidence))
            {
                var homeTown =
                    _locationService?
                        .GetLocation(head)
                        .HomeTown
                        .Town;

                if (!string.IsNullOrWhiteSpace(homeTown))
                {
                    lines.Insert(
                        0,
                        $"{homeTown} — Renting");
                }
            }

            return string.Join(
                Environment.NewLine,
                lines);
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
        AlbumYear <= 1900
            ? 1900
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
        $"Year {Math.Max(1900, _yearSummaryEventYear - 1)}";

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

    public ObservableCollection<AlbumEventViewModel>
        AlbumEvents { get; } = [];

    public ObservableCollection<YearSummaryHouseholdViewModel>
        YearSummaryHouseholds { get; } = [];

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

    public ObservableCollection<AvailableActionViewModel>
        PassActions { get; } = [];

    public ObservableCollection<ActionFilterViewModel>
        ActionFilters { get; } = [];

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
            RefreshChildHappiness();
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
                : $"Marriage: {snapshot.Label}";
        }
    }

    public string SelectedMarriageSatisfactionLabel
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

            return snapshot?.Label
                ?? string.Empty;
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

    public string SelectedChildHappinessText
    {
        get
        {
            var person = FindSelectedPerson();
            var snapshot = person is null
                ? null
                : _childHappinessService?.GetHappiness(person);
            return snapshot is null ? string.Empty : $"Happiness: {snapshot.Label}";
        }
    }

    public string SelectedChildHappinessLabel
    {
        get
        {
            var person = FindSelectedPerson();
            return person is null
                ? string.Empty
                : _childHappinessService?.GetHappiness(person)?.Label ?? string.Empty;
        }
    }

    public bool HasSelectedChildHappiness =>
        !string.IsNullOrWhiteSpace(SelectedChildHappinessText);

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
            OnPropertyChanged(
                nameof(IsStatusMessageVisible));
        }
    }

    public bool HasPersistenceStatus =>
        !string.IsNullOrWhiteSpace(
            PersistenceStatusText);

    public bool IsStatusMessageVisible =>
        HasPersistenceStatus;

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
    public RelayCommand ShowLivingFamilyCommand { get; }
    public RelayCommand ShowBloodlineFamilyCommand { get; }
    public RelayCommand ShowDeceasedFamilyCommand { get; }
    public RelayCommand PreviousAlbumYearCommand { get; }
    public RelayCommand NextAlbumYearCommand { get; }


}
