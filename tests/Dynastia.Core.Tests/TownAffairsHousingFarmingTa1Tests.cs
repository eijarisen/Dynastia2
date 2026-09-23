namespace Dynastia.Core.Tests;

public sealed class TownAffairsHousingFarmingTa1Tests
{
    [Fact]
    public void TownAffairsHubDefinesRequestedTabsContextAndRemoteMode()
    {
        var root = RepositoryFiles.Root;
        var context = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs"));
        var window = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "Views", "TownLifeWindow.axaml"));

        Assert.Contains("enum TownAffairsTab", context);
        Assert.Contains("enum TownAffairsMode", context);
        Assert.Contains("record TownAffairsRequest", context);
        Assert.Contains("class TownAffairsViewModel", context);
        Assert.Contains("RemoteHousingBrowse", context);
        Assert.DoesNotContain("Move here before using local services.", context);
        Assert.Contains("ShowJobsTab => !IsRemote", context);
        Assert.Contains("ShowHealthTab => !IsRemote", context);

        foreach (var tab in new[]
                 {
                     "Institutions", "Housing", "Jobs",
                     "Education", "Bank", "Health"
                 })
        {
            Assert.Contains($"Header=\"{tab}\"", window);
        }

        Assert.DoesNotContain("Header=\"Instructions\"", window);
        Assert.DoesNotContain("Instructions =", context);
    }


    [Fact]
    public void MainWindowUsesTheTownAffairsActionRouter()
    {
        var mainWindow = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "MainWindow.axaml.cs");
        Assert.Contains("CreateTownAffairsRequest(e.ActionId)", mainWindow);
    }


    [Fact]
    public void TabActionsPreserveExistingActionIdsAndRemoteModeCannotQueueThem()
    {
        var root = RepositoryFiles.Root;
        var hub = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs"));
        var education = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.CraftsEducation.cs"));
        var jobs = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.Opportunities.cs"));

        Assert.Contains("_owner.QueueJobApplication(JobActionId, opportunity, Subject)", hub);
        Assert.Contains("_owner.QueueEducationAction(optionId, Subject)", hub);
        Assert.Contains("_owner.QueueTownAffairsHealthAction(actionId, Subject)", hub);
        Assert.Contains("if (!ShowJobsTab", hub);
        Assert.Contains("\"education.get_education\"", education);
        Assert.Contains("_actionRegistry.Execute(\n            actionId", jobs);
    }

    [Fact]
    public void SelfImprovementSelectorIsRetiredAndServicesRouteThroughTownAffairs()
    {
        var root = RepositoryFiles.Root;
        var townAffairs = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs"));
        var mainWindow = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "Views", "MainWindow.axaml.cs"));

        Assert.DoesNotContain("ui.self_improvement", mainWindow);
        Assert.Contains("wellbeing.therapy", townAffairs);
        Assert.Contains("personality.religious_study", townAffairs);
        Assert.Contains("stats.improve_strength", townAffairs);
    }

    [Fact]
    public void ShortcutRouteMatrixIsInstalledForBatchOne()
    {
        var root = RepositoryFiles.Root;
        var path = Path.Combine(
            root, "data", "TownAffairs", "shortcut_routes.csv");
        var csv = File.ReadAllText(path);

        Assert.Contains("ui.town_affairs,institutions", csv);
        Assert.Contains("career.seek_employment,jobs", csv);
        Assert.Contains("education.get_education,education", csv);
        Assert.Contains("loan.take,bank", csv);
        Assert.Contains("loan.give,bank", csv);
        Assert.Contains("wellbeing.heal_relative,health", csv);
        Assert.Contains("wellbeing.therapy,health", csv);
    }

    [Fact]
    public void TownLifeSnapshotRemainsReadOnlyDomainInputToAppHub()
    {
        var root = RepositoryFiles.Root;
        var hub = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs"));
        var townLife = File.ReadAllText(Path.Combine(
            root, "plugins", "Dynastia.Mechanics.TownLife", "StandardTownLifeService.cs"));

        Assert.Contains("TownLifeSnapshot Snapshot", hub);
        Assert.DoesNotContain("ITownLifeService", hub);
        Assert.Contains("BuildSnapshot", townLife);
        Assert.DoesNotContain("IActionRegistry", townLife);
    }

}
