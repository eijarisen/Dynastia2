namespace Dynastia.Core.Tests;

public sealed class Development9UiRegressionTests
{
    [Fact]
    public void PartnerCardsStayCompactAndDoNotShowHobbies()
    {
        var root = RepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "Views",
            "PotentialPartnersWindow.axaml"));

        Assert.Contains("Height=\"170\"", xaml);
        Assert.DoesNotContain("HobbiesText", xaml);
    }

    [Fact]
    public void QueuedActionPresentationUsesEnDashAndCleanFarmlandLabels()
    {
        var root = RepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "ViewModels",
            "MainWindowViewModel.Actions.cs"));

        Assert.Contains("text += $\" – {detail}\"", source);
        Assert.Contains("return \"Buy Farmland\";", source);
        Assert.Contains("return \"Sell Farmland\";", source);
        Assert.DoesNotContain("text += $\" -- {detail}\"", source);
    }

    [Fact]
    public void CraftProfessionUsesQuitLabelAndSingleChoiceShortcut()
    {
        var root = RepositoryRoot();
        var crafts = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Crafts",
            "CraftsPlugin.cs"));
        var window = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "Views",
            "MainWindow.axaml.cs"));

        Assert.Contains("Label = \"Quit Profession\"", crafts);
        Assert.DoesNotContain("Label = \"Stop Working in a Profession\"", crafts);
        Assert.Contains("if (craftOptions.Count == 1)", window);
        Assert.Contains("craftOptions[0].Id", window);
    }

    [Fact]
    public void FamilyInventorySeparatesBudgetAndShowsCareerLevelContext()
    {
        var root = RepositoryRoot();
        var inventory = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "ViewModels",
            "MainWindowViewModel.Inventory.cs"));
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "Views",
            "FamilyInventoryWindow.axaml"));
        var ledger = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Economy",
            "LedgerLineState.cs"));
        var financeSystem = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Economy",
            "EconomyYearSystem.cs"));
        var economy = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Economy",
            "StandardEconomyService.cs"));

        Assert.Contains("(Level {career.JobLevel})", inventory);
        Assert.Contains("PeakJobLevel", inventory);
        Assert.Contains("Guid? PersonId", ledger);
        Assert.Contains("PersonId = line.PersonId", financeSystem);
        Assert.Contains("line.PersonId", economy);
        Assert.Contains("Margin=\"0,0,0,8\"", xaml);
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
