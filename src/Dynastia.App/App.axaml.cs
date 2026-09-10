using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Dynastia.App.ViewModels;
using Dynastia.App.Views;
using Dynastia.Core.Plugins;
using Dynastia.PluginHost;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

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
            var gameState = new GameState();

            var father =
                gameState.CreatePerson(
                    "Jan",
                    "Kowalski",
                    45);

            father.Tags.Add("state.dead");

            var mother =
                gameState.CreatePerson(
                    "Anna",
                    "Kowalski",
                    42);

            mother.Tags.Add("state.dead");

            var founder =
                gameState.CreatePerson(
                    "Piotr",
                    "Kowalski",
                    18);

            founder.Tags.Add("state.alive");
            founder.Tags.Add("age.adult");
            founder.Tags.Add("family.bloodline");
            founder.Tags.Add("lineage.male");

            var yearSystemRegistry =
                new YearSystemRegistry();

            var pluginContext =
                new GamePluginContext();

            pluginContext.AddService<IGameState>(
                gameState);

            pluginContext.AddService<IYearSystemRegistry>(
                yearSystemRegistry);

            var pluginLoader =
                new PluginLoader();

            var pluginsDirectory = Path.Combine(
                AppContext.BaseDirectory,
                "Plugins");

            var loadedPlugins =
                pluginLoader.LoadPlugins(
                    pluginsDirectory,
                    pluginContext);

            var yearProcessor =
                new YearProcessor(
                    gameState,
                    yearSystemRegistry);



            var mainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };

            mainWindow.Title =
                $"Dynastia — {loadedPlugins.Count} plugin(s) loaded";

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}