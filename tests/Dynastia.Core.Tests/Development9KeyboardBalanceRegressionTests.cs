using Dynastia.Contracts;

namespace Dynastia.Core.Tests;

public sealed class Development9KeyboardBalanceRegressionTests
{
    [Fact]
    public void NewGameStartYearExtendsThrough2000()
    {
        Assert.Equal(2000, GameCalendarConfiguration.MaximumSelectableStartYear);
        Assert.Equal(2000, GameCalendarConfiguration.NormalizeSelectableStartYear(2004));

        var root = RepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "Views",
            "MainWindow.axaml"));

        Assert.Contains("Maximum=\"2000\"", xaml);
    }

    [Fact]
    public void SharedSelectionWindowAcceptsEnterAndClosesOnEscape()
    {
        var root = RepositoryRoot();
        var code = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "Views",
            "PropertySelectionWindow.axaml.cs"));

        Assert.Contains("InputElement.KeyDownEvent", code);
        Assert.Contains("RoutingStrategies.Tunnel", code);
        Assert.Contains("e.Key == Key.Escape", code);
        Assert.Contains("e.Key != Key.Enter", code);
        Assert.Contains("Close(selected.Id)", code);
    }

    [Fact]
    public void MainWindowRegistersRequestedGameplayShortcuts()
    {
        var root = RepositoryRoot();
        var code = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "Views",
            "MainWindow.axaml.cs"));

        foreach (var key in new[]
        {
            "Key.M", "Key.T", "Key.R", "Key.P", "Key.F", "Key.E",
            "Key.I", "Key.S", "Key.C", "Key.J", "Key.H", "Key.Q", "Key.Space"
        })
        {
            Assert.Contains(key, code);
        }

        Assert.Contains("viewModel.HasQueuedAction", code);
        Assert.Contains("HideMainMenuPromptCommand.Execute(null)", code);
        Assert.Contains("ReturnToMainMenuCommand.Execute(null)", code);
        Assert.Contains("wellbeing.heal_relative", code);
        Assert.Contains("craft.stop_occupation", code);
        Assert.Contains("turn.pass", code);
    }

    [Fact]
    public void FirstParallaxLayerMovesMoreSlowly()
    {
        var root = RepositoryRoot();
        var menu = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "Controls",
            "TreeParallaxBackground.cs"));
        var tree = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "Genealogy",
            "Rendering",
            "GenealogyCanvas.cs"));

        Assert.Contains("DrawParallax(context, _parallax1, 0.0075);", menu);
        Assert.Contains("Parallax1Factor =\n        0.0075;", tree);
    }

    [Fact]
    public void HistoricalNannyLabelReflectsTripledAnnualCost()
    {
        var root = RepositoryRoot();
        var historical = File.ReadAllText(Path.Combine(
            root,
            "data",
            "Common",
            "historical_action_variants.json"));

        Assert.Contains("Hire a Nursemaid (750 zł/year)", historical);
        Assert.Contains("Hire a Nanny (750 zł/year)", historical);
        Assert.DoesNotContain("(250 zł/year)", historical);
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dynastia.slnx")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository root.");
    }
}
