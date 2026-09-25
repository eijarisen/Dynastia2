using Dynastia.App.ViewModels.Actions;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Dynastia.App.Audio;
using Dynastia.App.Genealogy.Host;
using Dynastia.App.Map.Host;
using Dynastia.App.Persistence;
using Dynastia.App.ViewModels;
using Dynastia.App.Views;
using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Data;
using Dynastia.Core.Events;
using Dynastia.Core.Plugins;
using Dynastia.Core.Simulation;
using Dynastia.PluginHost;

namespace Dynastia.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var gameRandom =
                new GameRandom();

            var gameState =
                new GameState(gameRandom);

            var registry =
                new YearSystemRegistry();

            var selectionService =
                new SelectionService();

            var gameCalendar =
                new GameCalendar();

            var eventBus =
                new GameEventBus();

            var dataDirectory =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Data");

            var dataService =
                new JsonGameDataService(
                    dataDirectory);

            var actionGuardRegistry =
                new ActionGuardRegistry();

            var reconciliation =
                new StateReconciliationLifecycle();

            var actionRegistry =
                new ActionRegistry(
                    gameState,
                    eventBus,
                    gameRandom,
                    actionGuardRegistry,
                    reconciliation);

            registry.Register(
                new TurnStartQueuedActionYearSystem(actionRegistry));

            registry.Register(
                new QueuedActionYearSystem(
                    actionRegistry,
                    YearPhase.QueuedActionsEarly,
                    "actions.queued.early"));

            registry.Register(
                new QueuedActionYearSystem(
                    actionRegistry,
                    YearPhase.LifeEvents,
                    "actions.queued.life_events"));

            registry.Register(
                new QueuedActionYearSystem(
                    actionRegistry,
                    YearPhase.MarriageRepair,
                    "actions.queued.marriage_repair"));

            registry.Register(
                new QueuedActionYearSystem(
                    actionRegistry,
                    YearPhase.MoralsReflection,
                    "actions.queued.morals_reflection"));

            registry.Register(
                new QueuedActionYearSystem(
                    actionRegistry,
                    YearPhase.FamilyRelationActions,
                    "actions.queued.family_relations"));

            var pluginContext =
                new GamePluginContext();

            pluginContext.AddService<IGameState>(
                gameState);

            pluginContext.AddService<IPersonLookup>(
                gameState);

            pluginContext.AddService<IYearSystemRegistry>(
                registry);

            pluginContext.AddService<ISelectionService>(
                selectionService);

            pluginContext.AddService<IGameRandom>(
                gameRandom);

            pluginContext.AddService<IGameCalendar>(
                gameCalendar);

            pluginContext.AddService<IGameEventBus>(
                eventBus);

            pluginContext.AddService<IGameDataService>(
                dataService);

            pluginContext.AddService<IContextWeightService>(
                new ContextWeightService(dataService));

            pluginContext.AddService<IActionGuardRegistry>(
                actionGuardRegistry);

            pluginContext.AddService<IActionRegistry>(
                actionRegistry);

            pluginContext.AddService<IStateReconciliationLifecycle>(
                reconciliation);

            var pluginsDirectory =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "plugins");

            var pluginLoader =
                new PluginLoader();

            pluginLoader.LoadPlugins(
                pluginsDirectory,
                pluginContext);

            var newGameService =
                pluginContext.GetService<INewGameService>()
                ?? throw new InvalidOperationException(
                    "No New Game service was registered. " +
                    "Is dynastia.family installed?");

            var successionService =
                pluginContext.GetService<ISuccessionService>()
                ?? throw new InvalidOperationException(
                    "No Succession service was registered. " +
                    "Is dynastia.succession installed?");

            var statsService =
                pluginContext.GetService<IStatsService>();

            var familyService =
                pluginContext.GetService<IFamilyService>();

            var nationalityService =
                pluginContext.GetService<INationalityService>();

            var healthService =
                pluginContext.GetService<IHealthService>();

            var stressService =
                pluginContext.GetService<IStressService>();

            var economyService =
                pluginContext.GetService<IEconomyService>();

            var houseMarketService =
                pluginContext.GetService<IHouseMarketService>();

            var townProsperityService =
                pluginContext.GetService<ITownProsperityService>();

            var farmingService =
                pluginContext.GetService<IFarmingService>();

            var craftService =
                pluginContext.GetService<ICraftService>();

            var loanService =
                pluginContext.GetService<ILoanService>();

            var heirloomService =
                pluginContext.GetService<IHeirloomService>();

            var statusService =
                pluginContext.GetService<IStatusService>();

            var householdService =
                pluginContext.GetService<IHouseholdService>();

            var gameScoreService =
                pluginContext.GetService<IGameScoreService>();


            var autonomousHouseholdDecisionService =
                pluginContext.GetService<
                    IAutonomousHouseholdDecisionService>();

            var adoptionService =
                pluginContext.GetService<IAdoptionService>();

            var locationService =
                pluginContext.GetService<ILocationService>();

            var localCareerOpportunityService =
                pluginContext.GetService<ILocalCareerOpportunityService>();

            var townLifeService =
                pluginContext.GetService<ITownLifeService>();

            var marriageSatisfactionService =
                pluginContext.GetService<
                    IMarriageSatisfactionService>();

            var familyRelationService =
                pluginContext.GetService<
                    IFamilyRelationService>();

            var householdConnectionService =
                pluginContext.GetService<
                    IHouseholdConnectionService>();

            var thoughtService =
                pluginContext.GetService<
                    IThoughtService>();

            var hobbyService =
                pluginContext.GetService<
                    IHobbyService>();

            var personalityService =
                pluginContext.GetService<
                    IPersonalityService>();

            var appearanceService =
                pluginContext.GetService<
                    IAppearanceService>();

            var childHappinessService =
                pluginContext.GetService<
                    IChildHappinessService>();

            var educationService =
                pluginContext.GetService<IEducationService>();

            var careerService =
                pluginContext.GetService<ICareerService>();

            var careerPresentationService =
                pluginContext.GetService<
                    ICareerPresentationService>();

            var partnerSearchService =
                pluginContext.GetService<IPartnerSearchService>();

            var justiceService =
                pluginContext.GetService<IJusticeService>();

            var biographyService =
                pluginContext.GetService<IBiographyService>();

            var historicalEventService =
                pluginContext.GetService<IHistoricalEventService>();

            var saveService =
                new GameSaveService(
                    gameState,
                    selectionService,
                    successionService,
                    eventBus,
                    actionRegistry,
                    biographyService,
                    gameRandom);

            var yearProcessor =
                new YearProcessor(
                    gameState,
                    registry,
                    saveService,
                    reconciliation,
                    actionRegistry);

            var genealogyDataSource =
                familyService is null
                    ? null
                    : new GameGenealogyDataSource(
                        gameState,
                        familyService,
                        eventBus,
                        healthService,
                        educationService,
                        careerService,
                        farmingService,
                        justiceService,
                        statsService,
                        locationService,
                        marriageSatisfactionService,
                        thoughtService,
                        appearanceService,
                        successionService,
                        stressService,
                        childHappinessService,
                        statusService);

            var genealogySelection =
                new SelectionServiceGenealogyAdapter(
                    selectionService);

            var mapDataSource =
                locationService is null
                    ? null
                    : new GameMapDataSource(
                        gameState,
                        locationService,
                        familyService,
                        economyService,
                        householdService,
                        successionService);

            var musicService =
                new BackgroundMusicService();

            var actionSelectionOptions = new ActionSelectionOptionService(
                successionService, actionRegistry, economyService, farmingService, heirloomService,
                loanService, locationService, townProsperityService, localCareerOpportunityService);
            var actionSurfaces = new ActionSurfaceDefinitions(
                locationService, economyService, justiceService);

            var mainWindow =
                new MainWindow
                {
                    GenealogyDataSource =
                        genealogyDataSource,

                    GenealogySelection =
                        genealogySelection,

                    MapDataSource =
                        mapDataSource,

                    TownLifeService =
                        townLifeService,

                    MusicService =
                        musicService,

                    DataContext =
                        new MainWindowViewModel(
                            gameState,
                            newGameService,
                            yearProcessor,
                            selectionService,
                            statsService,
                            familyService,
                            nationalityService,
                            healthService,
                            stressService,
                            economyService,
                            houseMarketService,
                            townProsperityService,
                            farmingService,
                            craftService,
                            loanService,
                            heirloomService,
                            statusService,
                            householdService,
                            autonomousHouseholdDecisionService,
                            adoptionService,
                            locationService,
                            localCareerOpportunityService,
                            marriageSatisfactionService,
                            familyRelationService,
                            householdConnectionService,
                            thoughtService,
                            hobbyService,
                            personalityService,
                            appearanceService,
                            childHappinessService,
                            educationService,
                            careerService,
                            careerPresentationService,
                            partnerSearchService,
                            justiceService,
                            biographyService,
                            historicalEventService,
                            successionService,
                            eventBus,
                            actionRegistry,
                            saveService,
                            reconciliation,
                            actionSelectionOptions,
                            actionSurfaces,
                            gameScoreService)
                };

            mainWindow.Opened +=
                (_, _) =>
                    musicService.Start();

            desktop.Exit +=
                (_, _) =>
                    musicService.Dispose();

            desktop.MainWindow =
                mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
