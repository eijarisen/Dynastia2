using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Dynastia.App.ViewModels;
using Dynastia.App.Views;
using Dynastia.Contracts;
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
            var gameState = new GameState
            {
                DynastySurname = "Kowalski",
                Year = 1900
            };

            var father = gameState.CreatePerson("Jan", "Kowalski", 45);
            father.Tags.Add("state.dead");

            var mother = gameState.CreatePerson("Anna", "Kowalski", 42);
            mother.Tags.Add("state.dead");

            var founder = gameState.CreatePerson("Piotr", "Kowalski", 18);
            founder.Tags.Add("state.alive");
            founder.Tags.Add("age.adult");
            founder.Tags.Add("family.bloodline");
            founder.Tags.Add("lineage.male");

            var registry = new YearSystemRegistry();

            var selectionService = new SelectionService
            {
                SelectedPersonId = founder.Id
            };

            var gameRandom = new GameRandom();
            var eventBus = new GameEventBus();

            var pluginContext = new GamePluginContext();

            pluginContext.AddService<IGameState>(gameState);
            pluginContext.AddService<IYearSystemRegistry>(registry);
            pluginContext.AddService<ISelectionService>(selectionService);
            pluginContext.AddService<IGameRandom>(gameRandom);
            pluginContext.AddService<IGameEventBus>(eventBus);

            var pluginsDirectory = Path.Combine(
                AppContext.BaseDirectory,
                "plugins");

            var pluginLoader = new PluginLoader();

            pluginLoader.LoadPlugins(
                pluginsDirectory,
                pluginContext);

            var yearProcessor =
                new YearProcessor(gameState, registry);

            var statsService =
                pluginContext.GetService<IStatsService>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    gameState,
                    yearProcessor,
                    selectionService,
                    statsService,
                    eventBus)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
