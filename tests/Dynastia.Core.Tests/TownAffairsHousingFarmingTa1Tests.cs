namespace Dynastia.Core.Tests;

public sealed class TownAffairsHousingFarmingTa1Tests
{
    [Fact]
    public void TownAffairsHubDefinesRequestedTabsContextAndRemoteMode()
    {
        var root = RepositoryRoot();
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
    public void DirectAccessUsesLivingAdultActiveHouseholdMembershipRatherThanControllability()
    {
        var root = RepositoryRoot();
        var townAffairs = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs"));

        Assert.Contains("target.Age < 18", townAffairs);
        Assert.Contains("target.Tags.Has(\"state.alive\")", townAffairs);
        Assert.Contains("GetHouseholdMemberIds(actor)", townAffairs);
        Assert.DoesNotContain("_succession.IsControllable(target)", townAffairs);
    }

    [Fact]
    public void ExistingLocalServiceActionsRouteToTheirTownAffairsTabs()
    {
        var root = RepositoryRoot();
        var townAffairs = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs"));
        var mainWindow = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "Views", "MainWindow.axaml.cs"));
        var actions = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.Actions.cs"));

        Assert.Contains("career.seek_employment\" => TownAffairsTab.Jobs", townAffairs);
        Assert.Contains("career.find_another_job\" => TownAffairsTab.Jobs", townAffairs);
        Assert.Contains("career.help_seek_employment\" => TownAffairsTab.Jobs", townAffairs);
        Assert.Contains("career.help_find_better_job\" => TownAffairsTab.Jobs", townAffairs);
        Assert.Contains("education.get_education\" => TownAffairsTab.Education", townAffairs);
        Assert.Contains("wellbeing.heal_relative\" => TownAffairsTab.Health", townAffairs);
        Assert.Contains("wellbeing.therapy\" => TownAffairsTab.Health", townAffairs);
        Assert.Contains("CreateTownAffairsRequest(e.ActionId)", mainWindow);
        Assert.Contains("wellbeing.heal_relative", actions);
        Assert.Contains("wellbeing.therapy", actions);
    }

    [Fact]
    public void HealthShortcutCanRouteAChildWithoutRelaxingDirectTownAffairsAgeRule()
    {
        var root = RepositoryRoot();
        var code = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs"));

        var directGate = code.IndexOf(
            "actionId.Equals(TownAffairsUiActionId",
            StringComparison.Ordinal);
        var ageGate = code.IndexOf("target.Age < 18", StringComparison.Ordinal);
        var healthRoute = code.IndexOf(
            "\"wellbeing.heal_relative\" => TownAffairsTab.Health",
            StringComparison.Ordinal);

        Assert.True(ageGate >= 0);
        Assert.True(healthRoute >= 0);
        Assert.True(directGate >= 0);
        Assert.Contains("&& !CanOpenTownAffairs", code);
    }

    [Fact]
    public void TabActionsPreserveExistingActionIdsAndRemoteModeCannotQueueThem()
    {
        var root = RepositoryRoot();
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
    public void SelfImprovementNoLongerDuplicatesTherapy()
    {
        var root = RepositoryRoot();
        var code = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.SelfImprovement.cs"));

        Assert.DoesNotContain("wellbeing.therapy", code);
        Assert.Contains("ReligiousStudyActionId", code);
        Assert.Contains("stats.improve_", code);
    }

    [Fact]
    public void ShortcutRouteMatrixIsInstalledForBatchOne()
    {
        var root = RepositoryRoot();
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
        var root = RepositoryRoot();
        var hub = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs"));
        var townLife = File.ReadAllText(Path.Combine(
            root, "plugins", "Dynastia.Mechanics.TownLife", "StandardTownLifeService.cs"));

        Assert.Contains("TownLifeSnapshot Snapshot", hub);
        Assert.DoesNotContain("ITownLifeService", hub);
        Assert.Contains("BuildSnapshot", townLife);
        Assert.DoesNotContain("IActionRegistry", townLife);
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

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
