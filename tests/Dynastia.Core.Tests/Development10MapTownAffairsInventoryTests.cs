namespace Dynastia.Core.Tests;

public sealed class Development10MapTownAffairsInventoryTests
{
    [Fact]
    public void TownAffairsInstitutionCardsUseCompactLeftAlignedLayout()
    {
        var xaml = ReadRepositoryFile(
            "src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        Assert.Contains("ColumnDefinitions=\"42,*\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Left\"", xaml);
        Assert.Contains("TextAlignment=\"Left\"", xaml);
        Assert.Contains("Text=\"{Binding Summary}\"", xaml);
        Assert.Contains("Text=\"{Binding ServiceText}\"", xaml);
        Assert.Contains("Text=\"{Binding CareerText}\"", xaml);
    }

    [Fact]
    public void OverlappingMapMarkersOfferAChooserAndMapDetailsUseTownAffairsData()
    {
        var control = ReadRepositoryFile(
            "src", "Dynastia.App", "Map", "Rendering", "TownMapControl.cs");
        var panel = ReadRepositoryFile(
            "src", "Dynastia.App", "Map", "Views", "TownMapPanel.cs");
        var window = ReadRepositoryFile(
            "src", "Dynastia.App", "Map", "Views", "TownMapWindow.cs");

        Assert.Contains("HitTestTowns", control);
        Assert.Contains("hits.Count > 1", control);
        Assert.Contains("TownSelectionRequested", control);
        Assert.Contains("Select location", panel);
        Assert.Contains("activateAfterSelection", panel);
        Assert.Contains("Foreground = Brushes.Black", panel);
        Assert.Contains("_townLife.GetTownLife(townId)", panel);
        Assert.Contains("Region:", panel);
        Assert.Contains("Settlement Type:", panel);
        Assert.Contains("Local Economy:", panel);
        Assert.Contains("Strong Fields:", panel);
        Assert.Contains("Regional Support:", panel);
        Assert.Contains("Shocks:", panel);
        Assert.Contains("Institutions:", panel);
        Assert.Contains("Level {institution.Tier}", panel);
        Assert.Contains("new TownMapPanel", window);
        Assert.Contains("townLife);", window);
    }

    [Fact]
    public void InventoryMoneyKeepsFinanceLinesLeftOfGraphAndUsesDarkerGuidesAndEmoji()
    {
        var xaml = ReadRepositoryFile(
            "src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml");
        var graph = ReadRepositoryFile(
            "src", "Dynastia.App", "Controls", "HouseholdBudgetGraph.cs");

        var budgetIndex = xaml.IndexOf("BudgetText", StringComparison.Ordinal);
        var incomeLinesIndex = xaml.IndexOf("ItemsSource=\"{Binding Incomes}\"", StringComparison.Ordinal);
        var expensesIndex = xaml.IndexOf("ItemsSource=\"{Binding Expenses}\"", StringComparison.Ordinal);
        var graphIndex = xaml.IndexOf("<ui:HouseholdBudgetGraph", StringComparison.Ordinal);

        Assert.True(budgetIndex >= 0);
        Assert.True(incomeLinesIndex > budgetIndex);
        Assert.True(expensesIndex > incomeLinesIndex);
        Assert.True(graphIndex > expensesIndex);
        Assert.Contains("💰 Loans", xaml);
        Assert.Contains("🎩 Lifestyle Spending", xaml);
        Assert.Contains("Color.FromArgb(70, 101, 70, 33)", graph);
    }

    private static string ReadRepositoryFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(parts)));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dynastia.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
