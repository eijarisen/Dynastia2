using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Simulation;

namespace Dynastia.Core.Tests;

public sealed class TurnStartActionCharacterizationTests
{
    [Fact]
    public void CommittedActionsExecuteInTheScheduledYearBeforeAgingAndOnlyOnce()
    {
        using var f = new RefactorFixture();
        var actor = f.Person(17);
        var actions = new ActionRegistry(f.State, f.Events, f.Random, new ActionGuardRegistry());
        var previews = new List<(bool Preview, int Year, int Age)>();
        var executions = new List<(int Year, int Age, YearPhase? Phase)>();
        actions.Register(new GameActionDefinition
        {
            Id = "test.before_birthday", Label = "Before birthday", Description = "Fixture",
            Mode = ActionExecutionMode.Queued, QueuePhase = YearPhase.LifeEvents,
            IsAvailable = context =>
            {
                previews.Add((context.IsSchedulingPreview, context.ScheduledExecutionYear, context.Actor.Age));
                return context.IsSchedulingPreview && context.ScheduledExecutionYear == 1901 && context.Actor.Age == 17;
            },
            Execute = context =>
            {
                Assert.False(context.IsSchedulingPreview);
                executions.Add((context.GameState.Year, context.Actor.Age, context.ExecutionPhase));
                Assert.Equal("kept", context.Parameters["selection"]);
                return new GameActionResult(true);
            }
        });
        var parameters = new Dictionary<string, string> { ["selection"] = "kept" };
        Assert.True(actions.Execute("test.before_birthday", actor, actor, parameters).Success);
        parameters["selection"] = "changed later";
        Assert.Equal((true, 1901, 17), Assert.Single(previews));
        var systems = new YearSystemRegistry();
        systems.Register(new AgingFixture(actor));
        systems.Register(new TurnStartQueuedActionYearSystem(actions));
        var years = new YearProcessor(f.State, systems, actions: actions);
        years.AdvanceYear();
        Assert.Equal((1901, 17, (YearPhase?)YearPhase.TurnStartActions), Assert.Single(executions));
        Assert.Equal(18, actor.Age);
        Assert.Single(previews); // Committed availability is not rerolled at execution.
        Assert.Empty(actions.GetAllQueuedActions());
        Assert.Equal(QueuedActionResultCategory.ExecutedSuccessfully, Assert.Single(actions.LastQueuedActionOutcomes).Category);
        years.AdvanceYear();
        Assert.Single(executions);
        Assert.Equal(19, actor.Age);
    }

    [Fact]
    public void CapturedQueueDoesNotConsumeActionsAddedAfterTheCapture()
    {
        using var f = new RefactorFixture();
        var first = f.Person(30);
        var second = f.Person(30);
        var actions = new ActionRegistry(f.State, f.Events, f.Random, new ActionGuardRegistry());
        var executed = new List<Guid>();
        actions.Register(new GameActionDefinition
        {
            Id = "test.action", Label = "Action", Description = "Fixture", Mode = ActionExecutionMode.Queued,
            IsAvailable = _ => true,
            Execute = context => { executed.Add(context.Actor.Id); return new GameActionResult(true); }
        });
        Assert.True(actions.Execute("test.action", first, first).Success);
        actions.CaptureTurnStartQueuedActions();
        Assert.True(actions.Execute("test.action", second, second).Success);
        actions.ExecuteTurnStartQueuedActions();
        Assert.Equal(first.Id, Assert.Single(executed));
        Assert.Equal(second.Id, Assert.Single(actions.GetAllQueuedActions()).ActorId);
    }

    private sealed class AgingFixture(IPerson actor) : IYearSystem
    {
        public string Id => "test.aging";
        public YearPhase Phase => YearPhase.Aging;
        public IReadOnlyCollection<string> Before => [];
        public IReadOnlyCollection<string> After => [];
        public void Execute(IGameState state) => actor.Age++;
    }
}
