namespace Dynastia.Core.Tests;

public sealed class Development13PostAdditionsRegressionTests
{
    [Fact]
    public void TutorSelectionUsesDistinctChildOptionsLocal()
    {
        var source = Read(
            "src", "Dynastia.App", "ViewModels",
            "MainWindowViewModel.CraftsEducation.cs");

        Assert.Contains("var childOptions = new List<PropertySelectionOption>();", source);
        Assert.Contains("return childOptions;", source);
    }

    [Fact]
    public void CriminalOccupationPublishesCrimeOutcomeWithoutSeparateIncomeNews()
    {
        var service = Read(
            "plugins", "Dynastia.Mechanics.Justice",
            "CriminalOccupationService.cs");
        var templates = Read(
            "data", "LocalSociety", "extension_news_templates.csv");

        Assert.DoesNotContain("justice.criminal_income", service);
        Assert.DoesNotContain("justice.criminal_income", templates);
        Assert.Contains("PublishCrimeEvent(", service);
        Assert.Contains("[\"proceeds\"] = proceeds.ToString", service);
    }

    [Fact]
    public void OverlapLocationEntriesUseCompletedClicksForReliableDoubleClickActivation()
    {
        var map = Read(
            "src", "Dynastia.App", "Map", "Views", "TownMapPanel.cs");

        Assert.Contains("button.Click +=", map);
        Assert.Contains("isRapidSecondClick", map);
        Assert.Contains("OnTownActivated(selectedTownId);", map);
        Assert.DoesNotContain("activateAfterSelection || e.ClickCount >= 2", map);
    }

    [Fact]
    public void LifeOfCrimeOccupationShowsOnlyArchetypeAndMastery()
    {
        var career = Read(
            "plugins", "Dynastia.Mechanics.Career", "StandardCareerService.cs");

        Assert.Contains("$\"{crime.ArchetypeName} · {crime.MasteryName}\"", career);
        Assert.DoesNotContain("Life of Crime — {crime.ArchetypeName}", career);
    }

    [Fact]
    public void DivorceCreatesAndPreservesIndependentPeripheralHouseholds()
    {
        var breakups = Read(
            "plugins", "Dynastia.Mechanics.Relationships",
            "RelationshipBreakupService.cs");
        var reconciliation = Read(
            "plugins", "Dynastia.Mechanics.Households",
            "StandardHouseholdService.RelationshipReconciliation.cs");
        var succession = Read(
            "plugins", "Dynastia.Mechanics.Households",
            "StandardHouseholdService.Succession.cs");

        Assert.Contains("Divorce always separates the former spouses into real residences.", breakups);
        Assert.Contains("_economy.EnsureIndependentHousehold(\n            parent,", breakups);
        Assert.Contains("Compatibility repair for saves created before divorced", reconciliation);
        Assert.Contains("_economy.EnsureIndependentHousehold(\n                        person,", reconciliation);
        Assert.Contains("MemberIds can be briefly stale during the divorce", reconciliation);
        Assert.Contains("&& !_economy.HasHousehold(\n                                person)", reconciliation);
        Assert.Contains("&& ResolveHouseholdHead(\n                                person)?.Id\n                                == formerPartner.Id", reconciliation);
        Assert.Contains("A living divorced spouse may head a non-playable peripheral", succession);
        Assert.Contains("HasDirectBloodlineMarriage(oldHead)", succession);
    }

    [Fact]
    public void PermanentInjuryHelperUsesItsParameterName()
    {
        var tests = Read(
            "tests", "Dynastia.Core.Tests", "Development13AdditionsTests.cs");

        Assert.Contains("Assert.Equal(newsworthy, condition.Newsworthy);", tests);
        Assert.DoesNotContain("Assert.Equal(newsworthiness", tests);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

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
