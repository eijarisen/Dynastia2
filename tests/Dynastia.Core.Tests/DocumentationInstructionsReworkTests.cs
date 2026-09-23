namespace Dynastia.Core.Tests;

public sealed class DocumentationInstructionsReworkTests
{
    [Fact]
    public void InstructionsUseCompactSevenTabOrientationGuide()
    {
        var instructions = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "InstructionsWindow.axaml");

        var expectedTabs = new[]
        {
            "Basics",
            "Family",
            "People",
            "Work &amp; Skills",
            "Wealth &amp; Property",
            "Town &amp; Society",
            "Risks &amp; History"
        };

        Assert.Equal(7, Count(instructions, "<TabItem Header="));
        foreach (var tab in expectedTabs)
            Assert.Contains($"<TabItem Header=\"{tab}\"", instructions);

        Assert.Contains("Classes=\"tipCard\"", instructions);
        Assert.Contains("Classes=\"guideTip\"", instructions);
        Assert.Contains("FontWeight=\"Bold\" Text=\"Annual turn. \"", instructions);
        Assert.Contains("FontWeight=\"Bold\" Text=\"Lineage and bloodline. \"", instructions);
        Assert.Contains("FontWeight=\"Bold\" Text=\"Town Affairs. \"", instructions);
        Assert.Contains("FontWeight=\"Bold\" Text=\"Historical events. \"", instructions);
        Assert.Contains("action descriptions and tooltips for current costs, requirements and chances", instructions);
    }

    [Fact]
    public void InstructionsPreserveExistingWindowAssetsAndCloseContract()
    {
        var instructions = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "InstructionsWindow.axaml");
        var codeBehind = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "InstructionsWindow.axaml.cs");

        Assert.Contains("xmlns:ui=\"using:Dynastia.App.Controls\"", instructions);
        Assert.Contains("/Assets/UI/instructions_paper.png", instructions);
        Assert.Contains("ui:ImageStateButton", instructions);
        Assert.Contains("Click=\"OnCloseClick\"", instructions);
        Assert.Contains("OnCloseClick", codeBehind);
    }

    [Fact]
    public void GameDesignDocumentationHasAllPlannedDomainFilesAndCurrentBaselineFacts()
    {
        var files = new[]
        {
            "00-Index.md",
            "01-Core-Loop-and-Control.md",
            "02-People-Family-and-Relationships.md",
            "03-Health-Wellbeing-and-Life-Course.md",
            "04-Work-Education-Crafts-and-Status.md",
            "05-Households-Economy-and-Property.md",
            "06-Towns-Institutions-and-Community.md",
            "07-Justice-Events-and-History.md",
            "08-Actions-and-UI-Surfaces.md",
            "09-Design-Decisions.md"
        };

        foreach (var file in files)
        {
            var path = Path.Combine(RepositoryFiles.Root, "docs", "GameDesign", file);
            Assert.True(File.Exists(path), $"Missing game-design document: {file}");
            Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(path)));
        }

        var core = RepositoryFiles.ReadText("docs", "GameDesign", "01-Core-Loop-and-Control.md");
        Assert.Contains("1700 through 2000", core);
        Assert.Contains("FamilyRelationActions", core);
        Assert.Contains("dynasty left Poland", core);

        var community = RepositoryFiles.ReadText("docs", "GameDesign", "06-Towns-Institutions-and-Community.md");
        Assert.Contains("Institutions\n2. Community\n3. Housing\n4. Jobs\n5. Health\n6. Church\n7. Education\n8. Bank\n9. Court", community);
        Assert.Contains("outstanding player lobby", community);
        Assert.Contains("Warm/Close", community);

        var actions = RepositoryFiles.ReadText("docs", "GameDesign", "08-Actions-and-UI-Surfaces.md");
        Assert.Contains("`church.attend`", actions);
        Assert.Contains("Town Affairs → Church only", actions);
        Assert.Contains("`personality.religious_study`", actions);
    }

    [Fact]
    public void ReadmeAndDevelopmentMapPointToDesignReferenceAndCurrentStartRange()
    {
        var readme = RepositoryFiles.ReadText("README.md");
        var map = RepositoryFiles.ReadText("docs", "DevelopmentMap.md");

        Assert.Contains("1700 to 2000 in 10-year steps", readme);
        Assert.Contains("docs/GameDesign/00-Index.md", readme);
        Assert.Contains("docs/GameDesign/00-Index.md", map);
    }

    private static int Count(string value, string needle) =>
        value.Split(needle, StringSplitOptions.None).Length - 1;

}
