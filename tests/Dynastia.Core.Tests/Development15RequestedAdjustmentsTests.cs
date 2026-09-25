namespace Dynastia.Core.Tests;

public sealed class Development15RequestedAdjustmentsTests
{
    [Fact]
    public void ChronicleScoreDeltaUsesTheSameTextColorAsItsEvent()
    {
        var window = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "MainWindow.axaml");

        Assert.DoesNotContain("Foreground=\"#806020\"", window);

        var summaryEvent = Slice(
            window,
            "FontFamily=\"Georgia\"\n                                                                                FontSize=\"16\"",
            "Text=\"{Binding ScoreDeltaText}\" />");
        Assert.Contains("Foreground=\"#46321C\"", summaryEvent);
    }

    [Fact]
    public void PotentialPartnerDescriptionHasTenPixelGapBeforeSkills()
    {
        var window = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "PotentialPartnersWindow.axaml");

        Assert.Contains("RowDefinitions=\"Auto,Auto\" RowSpacing=\"10\"", window);
    }

    [Fact]
    public void PrivateTutorRoutesToAlwaysVisibleEducationTabAlongsideHelpAndKeepsAffordabilityGate()
    {
        var education = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Education", "EducationPlugin.cs");
        var educationUi = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.CraftsEducation.cs");
        var improvements = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.StatImprovements", "StatImprovementsPlugin.cs");
        var surfaces = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "Actions", "ActionSurfaceDefinitions.cs");
        var townLife = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var townAffairs = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");

        var tutor = Slice(education, "Id = \"education.private_tutor\"", "Label = \"Hire Private Tutor\"");
        Assert.DoesNotContain("ShowInPrimaryActionList = false", tutor);
        Assert.Contains("PrivateTutorCost = 3000m", education);
        Assert.Contains("economy.CanAfford(actor, PrivateTutorCost)", education);
        Assert.DoesNotContain("HasLocalSchool", Slice(education, "private static GameActionDefinition CreatePrivateTutorAction", "private static GameActionDefinition CreateHelpLearningAction"));
        Assert.Contains("Id =\n                \"education.help_learning\"", education);

        var tutorOption = Slice(educationUi, "var tutorDefinition", "return childOptions;");
        Assert.Contains("if (evaluation.Available)", tutorOption);

        var selectionRouting = surfaces[surfaces.IndexOf(
            "internal static bool RequiresSelection",
            StringComparison.Ordinal)..];
        Assert.Contains("education.private_tutor", selectionRouting);
        Assert.Contains("\"education.private_tutor\" => TownAffairsTab.Education", townLife);
        Assert.Contains("public bool ShowEducationTab =>\n        !IsRemote;", townAffairs);
        Assert.Contains("ShowInPrimaryActionList = false", improvements);
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return source[startIndex..endIndex];
    }
}
