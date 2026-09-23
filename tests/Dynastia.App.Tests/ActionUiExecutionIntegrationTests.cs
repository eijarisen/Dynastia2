using System.Collections.Specialized;
using Dynastia.Contracts;

namespace Dynastia.App.Tests;

public sealed class ActionUiExecutionIntegrationTests
{
    [Theory]
    [InlineData("standard")]
    [InlineData("selection")]
    [InlineData("loan")]
    public void MainWindowSubmitsOnceRefreshesPeopleOnceAndForwardsQueueNotifications(string entryPoint)
    {
        using var f = new ActionPanelFixture();
        f.Register("test.standard", "household.buy_house", "loan.take");
        f.Select();
        var resets = 0;
        var notifications = new List<string?>();
        f.View.People.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) resets++; };
        f.View.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        if (entryPoint == "standard")
            f.View.AvailableActions.Single(a => a.Id == "test.standard").ExecuteCommand.Execute(null);
        else if (entryPoint == "selection")
            f.View.QueueActionWithSelection("household.buy_house", f.Economy.Residence.Id);
        else
            f.View.QueueLoanAction("loan.take", new(5000m, 2, "Bank", "town", "polish", 0.85m));
        Assert.Single(f.Registry.Submissions);
        Assert.Equal(1, resets);
        Assert.Single(f.Actions.GetQueuedActions(f.Head));
        Assert.True(f.View.HasQueuedAction);
        Assert.Empty(f.View.AvailableActions);
        Assert.Empty(f.View.ActionFilters);
        Assert.Contains(nameof(f.View.HasQueuedAction), notifications);
        Assert.Contains(nameof(f.View.QueuedActionText), notifications);
        Assert.Contains(nameof(f.View.ActionsEmptyText), notifications);
    }

    [Fact]
    public void DialogRequestForwardsMainWindowAsSenderAndDoesNotRefreshOrSubmit()
    {
        using var f = new ActionPanelFixture();
        f.Register("education.private_tutor");
        f.Select();
        var requests = 0;
        var resets = 0;
        f.View.People.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) resets++; };
        f.View.ActionSelectionRequested += (sender, request) =>
        {
            Assert.Same(f.View, sender);
            Assert.Equal("education.private_tutor", request.ActionId);
            requests++;
        };
        f.View.AvailableActions.Single(a => a.Id == "education.private_tutor").ExecuteCommand.Execute(null);
        Assert.Equal(1, requests);
        Assert.Equal(0, resets);
        Assert.Empty(f.Registry.Submissions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedAttemptsStillRefreshOnceAndOnlySelectionFailuresDisplayMessages(bool selection)
    {
        using var f = new ActionPanelFixture();
        var allowed = true;
        var id = selection ? "household.sell_house" : "test.standard";
        f.Actions.Register(new GameActionDefinition
        {
            Id = id, Label = id, Description = "Fixture",
            EvaluateAvailability = _ => allowed ? ActionEvaluationResult.Allowed()
                : ActionEvaluationResult.Denied("test.stale", "Stale choice"),
            Execute = _ => throw new InvalidOperationException("Rejected actions cannot run.")
        });
        f.Select();
        var standard = f.View.AvailableActions.FirstOrDefault(a => a.Id == id);
        var resets = 0;
        f.View.People.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) resets++; };
        allowed = false;
        if (selection) f.View.QueueActionWithSelection(id, "missing-property");
        else standard!.ExecuteCommand.Execute(null);
        Assert.Single(f.Registry.Submissions);
        Assert.Empty(f.Actions.GetQueuedActions(f.Head));
        Assert.Equal(1, resets);
        Assert.Equal(selection ? "Stale choice" : string.Empty, f.View.PersistenceStatusText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedChoosePromptClearsOnlyOnOrdinarySuccessfulSubmission(bool selection)
    {
        using var f = new ActionPanelFixture();
        f.Actions.Register(new GameActionDefinition
        {
            Id = "test.denied", Label = "Denied", Description = "Fixture",
            EvaluateAvailability = _ => ActionEvaluationResult.Denied("test.denied", "Choose an action for each household."),
            Execute = _ => throw new InvalidOperationException()
        });
        f.Register("turn.pass", "household.buy_house");
        f.Select();
        f.View.QueueActionWithSelection("test.denied", "unused");
        Assert.StartsWith("Choose ", f.View.PersistenceStatusText);
        if (selection) f.View.QueueActionWithSelection("household.buy_house", f.Economy.Residence.Id);
        else f.View.AvailableActions.Single(a => a.Id == "turn.pass").ExecuteCommand.Execute(null);
        Assert.Single(f.Actions.GetQueuedActions(f.Head));
        Assert.Equal(selection ? "Choose an action for each household." : string.Empty, f.View.PersistenceStatusText);
    }

    [Fact]
    public void CancelledQueueRestoresThePanelWithoutReexecutingTheAction()
    {
        using var f = new ActionPanelFixture();
        f.Register("turn.pass");
        f.Select();
        f.View.AvailableActions.Single(a => a.Id == "turn.pass").ExecuteCommand.Execute(null);
        f.View.CancelQueuedActionCommand.Execute(null);
        Assert.False(f.View.HasQueuedAction);
        Assert.Equal(string.Empty, f.View.QueuedActionText);
        Assert.Contains(f.View.AvailableActions, a => a.Id == "turn.pass");
        Assert.Empty(f.Actions.GetQueuedActions(f.Head));
        Assert.Single(f.Registry.Submissions);
    }
}
