using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;

namespace Dynastia.Core.Tests;

public sealed class SharedMechanics4CReconciliationTests
{
    [Fact]
    public void LifecycleRunsOnlyRegisteredStageInStableOrder()
    {
        var lifecycle = new StateReconciliationLifecycle();
        var calls = new List<string>();

        lifecycle.Register(
            "later",
            [ReconciliationLifecycleStage.AfterLoad],
            stage => calls.Add($"later:{stage}"),
            order: 20);
        lifecycle.Register(
            "earlier",
            [ReconciliationLifecycleStage.AfterLoad],
            stage => calls.Add($"earlier:{stage}"),
            order: 10);
        lifecycle.Register(
            "new-game-only",
            [ReconciliationLifecycleStage.AfterNewGame],
            stage => calls.Add($"new:{stage}"));

        lifecycle.Reconcile(ReconciliationLifecycleStage.AfterLoad);

        Assert.Equal(
            ["earlier:AfterLoad", "later:AfterLoad"],
            calls);
    }

    [Fact]
    public void YearProcessorOwnsBeforeAndAfterYearReconciliation()
    {
        var state = new GameState();
        var registry = new YearSystemRegistry();
        var lifecycle = new StateReconciliationLifecycle();
        var stages = new List<ReconciliationLifecycleStage>();

        lifecycle.Register(
            "test",
            [
                ReconciliationLifecycleStage.BeforeYear,
                ReconciliationLifecycleStage.AfterYear
            ],
            stages.Add);

        new YearProcessor(
            state,
            registry,
            reconciliation: lifecycle)
            .AdvanceYear();

        Assert.Equal(
            [
                ReconciliationLifecycleStage.BeforeYear,
                ReconciliationLifecycleStage.AfterYear
            ],
            stages);
    }

    [Fact]
    public void SuccessfulImmediateActionRunsImmediateReconciliation()
    {
        var state = new GameState();
        var actor = state.CreatePerson("Jan", "Test", 30);
        var events = new GameEventBus();
        var lifecycle = new StateReconciliationLifecycle();
        var reconciliations = 0;

        lifecycle.Register(
            "test",
            [ReconciliationLifecycleStage.AfterImmediateAction],
            _ => reconciliations++);

        var actions = new ActionRegistry(
            state,
            events,
            new GameRandom(1),
            new ActionGuardRegistry(),
            lifecycle);

        actions.Register(new GameActionDefinition
        {
            Id = "test.immediate",
            Label = "Immediate",
            Description = "Test",
            Mode = ActionExecutionMode.Immediate,
            QueuePhase = YearPhase.QueuedActionsEarly,
            IsAvailable = _ => true,
            Execute = _ => new GameActionResult(true)
        });

        Assert.True(actions.Execute("test.immediate", actor, actor).Success);
        var outcome = Assert.Single(actions.ExecuteQueued(YearPhase.QueuedActionsEarly));

        Assert.Equal(QueuedActionResultCategory.ExecutedSuccessfully, outcome.Category);
        Assert.Equal(1, reconciliations);
    }
}
