using Dynastia.App.ViewModels;
using Dynastia.App.ViewModels.Actions;
using Dynastia.Contracts;

namespace Dynastia.App.Tests;

public sealed class ActionPanelCoordinatorTests
{
    [Fact]
    public void StandardActionSubmitsOncePublishesOnceAndDoesNotRefreshItselfOrExecuteMechanicsEarly()
    {
        using var f = new ActionCoordinatorFixture();
        var effects = 0;
        f.Source.Actions.Register(new GameActionDefinition
        {
            Id = "test.standard", Label = "Standard", Description = "Fixture",
            Mode = ActionExecutionMode.Immediate, IsAvailable = _ => true,
            Execute = _ => { effects++; return new GameActionResult(true); }
        });
        f.Panel.Refresh(false);
        var choice = f.Panel.AvailableActions.Single(action => action.Id == "test.standard");
        choice.ExecuteCommand.Execute(null);
        Assert.Equal("test.standard", Assert.Single(f.Source.Registry.Submissions).ActionId);
        var result = Assert.Single(f.Results);
        Assert.True(result.Attempted);
        Assert.True(result.Success);
        Assert.True(result.StateMayHaveChanged);
        Assert.False(result.FromSelection);
        Assert.Empty(f.Requests);
        Assert.Equal(0, effects);
        Assert.Contains(choice, f.Panel.AvailableActions);
        Assert.False(f.Panel.HasQueuedAction); // host, not coordinator, decides when to refresh
        f.Panel.Refresh(false);
        Assert.True(f.Panel.HasQueuedAction);
        Assert.Empty(f.Panel.AvailableActions);
        Assert.Empty(f.Panel.ActionFilters);
        Assert.Empty(f.Panel.PassActions);
        f.Source.Actions.CaptureTurnStartQueuedActions();
        Assert.Single(f.Source.Actions.ExecuteTurnStartQueuedActions());
        Assert.Equal(1, effects);
    }

    [Fact]
    public void CategoryFiltersUseUnionKeepPassLastAndRetainSelectionAcrossRefresh()
    {
        using var f = new ActionCoordinatorFixture();
        f.Source.Register("career.work_harder", "wellbeing.recover", "education.private_tutor", "turn.pass");
        f.Panel.Refresh(false);
        Assert.Equal(new[] { "ui.town_affairs", "wellbeing.recover", "career.work_harder", "education.private_tutor", "turn.pass" },
            f.Panel.AvailableActions.Select(action => action.Id));
        foreach (var filter in f.Panel.ActionFilters.ToArray()) filter.ToggleCommand.Execute(null);
        Assert.Equal("turn.pass", Assert.Single(f.Panel.AvailableActions).Id);
        f.Panel.ActionFilters.Single(filter => filter.Category == ActionCategory.Finances).ToggleCommand.Execute(null);
        Assert.Equal(new[] { "career.work_harder", "turn.pass" }, f.Panel.AvailableActions.Select(action => action.Id));
        f.Panel.ActionFilters.Single(filter => filter.Category == ActionCategory.Personal).ToggleCommand.Execute(null);
        Assert.Equal(new[] { "ui.town_affairs", "wellbeing.recover", "career.work_harder", "turn.pass" },
            f.Panel.AvailableActions.Select(action => action.Id));
        f.Panel.Refresh(false);
        Assert.Equal(new[] { "ui.town_affairs", "wellbeing.recover", "career.work_harder", "turn.pass" },
            f.Panel.AvailableActions.Select(action => action.Id));
        f.Panel.ResetActionCategoryFilters();
        f.Panel.Refresh(false);
        Assert.Contains(f.Panel.AvailableActions, action => action.Id == "education.private_tutor");
        Assert.Empty(f.Source.Registry.Submissions);
    }

    [Theory]
    [InlineData("ui.town_affairs")]
    [InlineData("ui.manage_properties")]
    [InlineData("ui.manage_finances")]
    [InlineData("ui.craft_profession")]
    [InlineData("education.get_education")]
    [InlineData("education.private_tutor")]
    [InlineData("wellbeing.heal_relative")]
    [InlineData("wellbeing.therapy")]
    [InlineData("career.seek_employment")]
    [InlineData("career.find_another_job")]
    [InlineData("career.help_seek_employment")]
    [InlineData("career.help_find_better_job")]
    [InlineData("relationship.find_spouse")]
    [InlineData("relationship.marry_off_daughter")]
    [InlineData("relationship.marry_off_son")]
    [InlineData("stats.improve_strength")]
    [InlineData("stats.improve_intellect")]
    [InlineData("stats.improve_immunity")]
    [InlineData("stats.improve_appeal")]
    [InlineData("stats.improve_longevity")]
    [InlineData("stats.improve_fertility")]
    [InlineData("church.attend")]
    [InlineData("church.donate")]
    [InlineData("church.aid_poor_family")]
    [InlineData("church.ask_welfare")]
    [InlineData("personality.religious_study")]
    public void SecondaryChoiceRequestsExactActionWithoutSubmitting(string id)
    {
        using var f = new ActionCoordinatorFixture();
        if (id == "ui.manage_properties") f.Source.Register("household.buy_house");
        else if (id == "ui.manage_finances") f.Source.Register("loan.take");
        else if (id == "ui.craft_profession") f.Source.Register("craft.start.carpentry");
        else if (id != "ui.town_affairs")
        {
            f.Source.Actions.Register(new GameActionDefinition
            {
                Id = id, Label = id, Description = "Fixture", IsAvailable = _ => true,
                // Explicit metadata makes normally hidden Town Affairs choices testable here.
                Presentation = new() { Categories = [ActionPresentationCategories.Personal] },
                Execute = _ => throw new InvalidOperationException("A dialog request must not execute mechanics.")
            });
        }
        f.Panel.Refresh(false);
        f.Click(id);
        Assert.Equal(id, Assert.Single(f.Requests));
        Assert.Empty(f.Source.Registry.Submissions);
        Assert.Empty(f.Source.Actions.GetQueuedActions(f.Source.Head));
        Assert.Empty(f.Results);
    }

    [Theory]
    [InlineData("household.buy_house")]
    [InlineData("household.sell_house")]
    [InlineData("loan.take")]
    [InlineData("loan.give")]
    public void AggregatedUnderlyingChoicesKeepTheirSecondarySelectionRoute(string id)
    {
        Assert.True(ActionSurfaceDefinitions.RequiresSelection(id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MoveOutPreservesRentalSingleHouseAndMultipleHouseBranches(int spareCount)
    {
        using var f = new ActionCoordinatorFixture();
        var resident = f.Source.Person(21, "Resident");
        f.Selected = resident;
        f.Source.Economy.Members.Add(resident.Id);
        f.Source.Economy.Houses.Add(new HousePropertyInfo(Guid.NewGuid(), f.Source.Economy.Residence, true, false));
        for (var i = 0; i < spareCount; i++)
            f.Source.Economy.Houses.Add(new HousePropertyInfo(Guid.NewGuid(), f.Source.Economy.Residence, false, true));
        f.Source.Register("household.ask_move_out");
        f.Panel.Refresh(false);
        f.Click("household.ask_move_out");
        if (spareCount > 1)
        {
            Assert.Equal("household.ask_move_out", Assert.Single(f.Requests));
            Assert.Empty(f.Source.Registry.Submissions);
            Assert.Empty(f.Results);
            Assert.Empty(f.Source.Actions.GetQueuedActions(f.Source.Head));
            return;
        }
        var submission = Assert.Single(f.Source.Registry.Submissions);
        var queued = Assert.Single(f.Source.Actions.GetQueuedActions(f.Source.Head));
        Assert.Equal(f.Source.Head.Id, submission.ActorId);
        Assert.Equal(resident.Id, submission.TargetId);
        Assert.Equal(resident.Id, queued.TargetId);
        Assert.Empty(f.Requests);
        Assert.Equal(spareCount == 1, Assert.Single(f.Results).FromSelection);
        if (spareCount == 0)
        {
            Assert.Null(submission.Parameters);
            Assert.Empty(queued.Parameters!);
        }
        else
        {
            Assert.Equal(f.Source.Economy.Houses[1].Id.ToString(), submission.Parameters!["propertyId"]);
            Assert.Equal("Z Residence", submission.Parameters["summaryTown"]);
            Assert.Equal(2, submission.Parameters.Count);
        }
    }

    [Fact]
    public void ShortcutsSearchUnfilteredChoicesRespectGameAndMenuGuardsAndStopAfterFirstMatch()
    {
        using var f = new ActionCoordinatorFixture();
        f.Source.Register("career.seek_employment", "career.find_another_job", "turn.pass");
        f.Panel.Refresh(false);
        foreach (var filter in f.Panel.ActionFilters.ToArray()) filter.ToggleCommand.Execute(null);
        Assert.Equal("turn.pass", Assert.Single(f.Panel.AvailableActions).Id);
        Assert.False(f.Panel.TryExecuteAvailableActionShortcut(false, false, "career.seek_employment"));
        Assert.False(f.Panel.TryExecuteAvailableActionShortcut(true, true, "career.seek_employment"));
        Assert.False(f.Panel.TryExecuteAvailableActionShortcut(true, false));
        Assert.False(f.Panel.TryExecuteAvailableActionShortcut(true, false, "missing"));
        Assert.True(f.Panel.TryExecuteAvailableActionShortcut(true, false,
            "missing", "CAREER.SEEK_EMPLOYMENT", "career.find_another_job"));
        Assert.Equal("career.seek_employment", Assert.Single(f.Requests));
        Assert.Empty(f.Source.Registry.Submissions);
    }

    [Fact]
    public void StaleCommandsResolveCurrentSelectedPersonAndIgnoreMissingSelectionOrGameOver()
    {
        using var f = new ActionCoordinatorFixture();
        f.Source.Register("test.standard");
        f.Panel.Refresh(false);
        var command = f.Panel.AvailableActions.Single(action => action.Id == "test.standard").ExecuteCommand;
        f.Selected = null;
        command.Execute(null);
        f.Panel.QueueActionWithSelection("household.ask_move_out", "property");
        Assert.Empty(f.Source.Registry.Submissions);
        Assert.Empty(f.Results);
        f.Selected = f.Source.Person(25, "New target");
        f.Source.Succession.IsGameOver = true;
        command.Execute(null);
        f.Panel.QueueLoanAction("loan.take", new(5000m, 2, "Bank", "town", "polish"));
        Assert.Empty(f.Source.Registry.Submissions);
        Assert.Empty(f.Results);
        f.Source.Succession.IsGameOver = false;
        command.Execute(null);
        Assert.Equal(f.Selected.Id, Assert.Single(f.Source.Registry.Submissions).TargetId);
    }

    [Fact]
    public void BloodlineViewHidesAllChoicesAndRetainsIndependentHouseholdMessages()
    {
        using var f = new ActionCoordinatorFixture();
        f.Source.Register("turn.pass");
        f.Panel.Refresh(false);
        Assert.NotEmpty(f.Panel.AvailableActions);
        f.Panel.Refresh(true);
        Assert.Empty(f.Panel.AvailableActions);
        Assert.Empty(f.Panel.PassActions);
        Assert.Empty(f.Panel.ActionFilters);
        Assert.False(f.Panel.HasQueuedAction);
        Assert.Equal("No autonomous bloodline households.", f.Panel.GetEmptyText(true, false));
        Assert.Equal("This household lives independently.", f.Panel.GetEmptyText(true, true));
        Assert.False(f.Panel.TryExecuteAvailableActionShortcut(true, false, "turn.pass"));
    }

    [Fact]
    public void GenericHiddenMetadataIsAppliedBeforeAggregation()
    {
        using var f = new ActionCoordinatorFixture();
        foreach (var id in new[] { "plugin.hidden", "household.buy_house", "loan.take", "craft.start.writing" })
        {
            f.Source.Actions.Register(new GameActionDefinition
            {
                Id = id, Label = id, Description = "Hidden", IsAvailable = _ => true,
                Presentation = new() { ShowInPrimaryActionList = false }, Execute = _ => new GameActionResult(true)
            });
        }
        f.Source.Register("turn.pass");
        f.Panel.Refresh(false);
        Assert.Equal(new[] { "ui.town_affairs", "turn.pass" }, f.Panel.AvailableActions.Select(action => action.Id));
    }
}
