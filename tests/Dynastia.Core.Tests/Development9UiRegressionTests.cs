namespace Dynastia.Core.Tests;

public sealed class Development9UiRegressionTests
{
    [Fact]
    public void PartnerCardsStayCompactAndDoNotShowHobbies()
    {
        var root = RepositoryFiles.Root;
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "Views",
            "PotentialPartnersWindow.axaml"));

        var opportunities = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "ViewModels",
            "MainWindowViewModel.Opportunities.cs"));

        Assert.Contains("<primitives:UniformGrid Columns=\"2\" Rows=\"2\" />", xaml);
        Assert.Contains("PointerPressed=\"OnCandidatePointerPressed\"", xaml);
        Assert.Contains("Grid.Row=\"1\"", xaml);
        Assert.Contains("RenownStatusText", xaml);
        Assert.Contains("ReputationStatusText", xaml);
        Assert.DoesNotContain("StatusText", xaml.Replace("RenownStatusText", string.Empty).Replace("ReputationStatusText", string.Empty));
        Assert.DoesNotContain("ApproachText", xaml);
        Assert.DoesNotContain("HobbiesText", xaml);
        Assert.Contains("count: 4", opportunities);
    }


    [Fact]
    public void CraftProfessionUsesQuitLabelAndSingleChoiceShortcut()
    {
        var root = RepositoryFiles.Root;
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
        var root = RepositoryFiles.Root;
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
        Assert.Contains("<ui:HouseholdBudgetGraph", xaml);
        Assert.Contains("History=\"{Binding BudgetHistory}\"", xaml);
        Assert.Contains("Text=\"{Binding BudgetText}\"", xaml);
    }

    [Fact]
    public void ImprisonedCareerAndStatusEmojiPresentationAreDistinct()
    {
        var root = RepositoryFiles.Root;
        var career = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "ViewModels",
            "CareerViewModel.cs"));
        var eventEmoji = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "ViewModels",
            "EventEmojiMap.cs"));

        Assert.Contains("isAlive && isImprisoned", career);
        Assert.Contains("? \"N/A\"", career);
        Assert.DoesNotContain("🔹", eventEmoji);
        Assert.Contains("[\"historical.milestone\"] = \"🗞️\"", eventEmoji);
        Assert.Contains("[\"career.changed_job\"] = \"🔄\"", eventEmoji);
        Assert.Contains("[\"farming.income\"] = \"🌾\"", eventEmoji);
    }

    [Fact]
    public void HistoricalEraWrapsAboveYearWithoutEnteringNextYearButton()
    {
        var root = RepositoryFiles.Root;
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "Views",
            "MainWindow.axaml"));

        Assert.Contains("Text=\"{Binding HistoricalEraName}\"", xaml);
        Assert.Contains("TextWrapping=\"Wrap\"", xaml);
        Assert.Contains("MaxLines=\"2\"", xaml);
        Assert.Contains("Canvas.Left=\"1158\"", xaml);
        Assert.Contains("Width=\"158\"", xaml);
    }

}
