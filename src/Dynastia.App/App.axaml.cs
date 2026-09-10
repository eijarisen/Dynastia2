using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            var gameState =
                new GameState();

            var registry =
                new YearSystemRegistry();

            var selectionService =
                new SelectionService();

            var gameRandom =
                new GameRandom();

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

            var actionRegistry =
                new ActionRegistry(
                    gameState,
                    eventBus,
                    gameRandom);

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

            var pluginContext =
                new GamePluginContext();

            pluginContext.AddService<IGameState>(
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

            pluginContext.AddService<IActionRegistry>(
                actionRegistry);

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

            var yearProcessor =
                new YearProcessor(
                    gameState,
                    registry);

            var statsService =
                pluginContext.GetService<IStatsService>();

            var familyService =
                pluginContext.GetService<IFamilyService>();

            var healthService =
                pluginContext.GetService<IHealthService>();

            desktop.MainWindow =
                new MainWindow
                {
                    DataContext =
                        new MainWindowViewModel(
                            gameState,
                            newGameService,
                            yearProcessor,
                            selectionService,
                            statsService,
                            familyService,
                            healthService,
                            successionService,
                            eventBus,
                            actionRegistry)
                };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
