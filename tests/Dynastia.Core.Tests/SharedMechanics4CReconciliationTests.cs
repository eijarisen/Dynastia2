using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Justice;

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
    [Fact]
    public void SuccessfulQueuedActionReconcilesPeopleCreatedDuringTheYear()
    {
        var state = new GameState();
        var actor = state.CreatePerson("Jan", "Test", 30);
        var events = new GameEventBus();
        var lifecycle = new StateReconciliationLifecycle();
        var justice = new StandardJusticeService();
        IPerson? created = null;

        lifecycle.Register(
            "justice.components",
            Enum.GetValues<ReconciliationLifecycleStage>(),
            _ =>
            {
                foreach (var person in state.People)
                    justice.EnsureJustice(person);
            });

        var actions = new ActionRegistry(
            state,
            events,
            new GameRandom(1),
            new ActionGuardRegistry(),
            lifecycle);

        actions.Register(new GameActionDefinition
        {
            Id = "test.create_person",
            Label = "Create Person",
            Description = "Test",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            IsAvailable = _ => true,
            Execute = context =>
            {
                created = context.GameState.CreatePerson("Anna", "Test", 25);
                return new GameActionResult(true);
            }
        });

        Assert.True(actions.Execute("test.create_person", actor, actor).Success);
        var outcome = Assert.Single(actions.ExecuteQueued(YearPhase.LifeEvents));

        Assert.Equal(QueuedActionResultCategory.ExecutedSuccessfully, outcome.Category);
        Assert.NotNull(created);
        Assert.False(justice.IsImprisoned(created!));
    }

    [Fact]
    public void YearProcessorReconcilesPeopleCreatedByYearSystemsBeforeLaterSystems()
    {
        var state = new GameState();
        var registry = new YearSystemRegistry();
        var lifecycle = new StateReconciliationLifecycle();
        var justice = new StandardJusticeService();
        IPerson? created = null;

        lifecycle.Register(
            "justice.components",
            Enum.GetValues<ReconciliationLifecycleStage>(),
            _ =>
            {
                foreach (var person in state.People)
                    justice.EnsureJustice(person);
            });

        registry.Register(
            new PersonCreationYearSystem(
                () => created = state.CreatePerson("Anna", "Test", 25)));

        registry.Register(
            new JusticeReadYearSystem(
                () => created,
                justice));

        new YearProcessor(
            state,
            registry,
            reconciliation: lifecycle)
            .AdvanceYear();

        Assert.NotNull(created);
        Assert.False(justice.IsImprisoned(created!));
    }

    private sealed class PersonCreationYearSystem : IYearSystem
    {
        private readonly Action _createPerson;

        public PersonCreationYearSystem(Action createPerson)
        {
            _createPerson = createPerson;
        }

        public string Id => "test.person_creation";
        public YearPhase Phase => YearPhase.LifeEvents;
        public IReadOnlyCollection<string> Before => [];
        public IReadOnlyCollection<string> After => [];

        public void Execute(IGameState gameState) => _createPerson();
    }

    private sealed class JusticeReadYearSystem : IYearSystem
    {
        private readonly Func<IPerson?> _person;
        private readonly IJusticeService _justice;

        public JusticeReadYearSystem(
            Func<IPerson?> person,
            IJusticeService justice)
        {
            _person = person;
            _justice = justice;
        }

        public string Id => "test.justice_read";
        public YearPhase Phase => YearPhase.PostYear;
        public IReadOnlyCollection<string> Before => [];
        public IReadOnlyCollection<string> After => [];

        public void Execute(IGameState gameState)
        {
            var person = _person()
                ?? throw new InvalidOperationException("The test person was not created.");

            _ = _justice.IsImprisoned(person);
        }
    }

}
