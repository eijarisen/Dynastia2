using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Entities;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;

namespace Dynastia.Core.Tests;

public sealed class ReliabilityBatchTests
{
    [Fact]
    public void GameRandomStateRestoresExactContinuation()
    {
        var random = new GameRandom(12345);
        _ = random.NextInt(1, 1000);
        _ = random.NextDouble();

        var state = random.CaptureState();
        var expected = Enumerable.Range(0, 20)
            .Select(_ => random.NextInt(-100000, 100000))
            .ToArray();

        random.RestoreState(state);
        var actual = Enumerable.Range(0, 20)
            .Select(_ => random.NextInt(-100000, 100000))
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GameStateUsesDeterministicPersonIdsWhenGameRandomIsProvided()
    {
        var first = new GameState(new GameRandom(42));
        var second = new GameState(new GameRandom(42));

        var firstIds = Enumerable.Range(0, 5)
            .Select(index => first.CreatePerson($"A{index}", "Test", 18).Id)
            .ToArray();
        var secondIds = Enumerable.Range(0, 5)
            .Select(index => second.CreatePerson($"B{index}", "Test", 18).Id)
            .ToArray();

        Assert.Equal(firstIds, secondIds);
    }

    [Fact]
    public void MissingYearDependencyFailsBeforeYearMutation()
    {
        var state = new GameState();
        var registry = new YearSystemRegistry();
        registry.Register(new TestSystem(
            "test.one",
            YearPhase.LifeEvents,
            _ => { },
            after: ["missing.system"]));

        var processor = new YearProcessor(state, registry);

        var exception = Assert.Throws<InvalidOperationException>(
            processor.AdvanceYear);

        Assert.Contains("missing.system", exception.Message);
        Assert.Equal(GameCalendarConfiguration.GameStartYear, state.Year);
    }

    [Fact]
    public void YearFailureRestoresPreYearState()
    {
        var state = new GameState();
        var person = state.CreatePerson("Jan", "Test", 20);
        var registry = new YearSystemRegistry();

        registry.Register(new TestSystem(
            "a.mutate",
            YearPhase.Aging,
            gameState =>
            {
                gameState.People[0].Age++;
            }));
        registry.Register(new TestSystem(
            "b.throw",
            YearPhase.Aging,
            _ => throw new InvalidOperationException("boom"),
            after: ["a.mutate"]));

        var boundary = new TestBoundary(state);
        var processor = new YearProcessor(state, registry, boundary);

        var exception = Assert.Throws<InvalidOperationException>(
            processor.AdvanceYear);

        Assert.Contains("b.throw", exception.Message);
        Assert.Equal(GameCalendarConfiguration.GameStartYear, state.Year);
        Assert.Equal(20, person.Age);
    }

    [Fact]
    public void InvalidatedQueuedActionProducesStructuredOutcomeWithoutChronicleNews()
    {
        var state = new MutableGameState();
        var actor = state.CreatePerson("Jan", "Test", 30);
        var target = state.CreatePerson("Anna", "Test", 28);
        target.Tags.Add("eligible");

        var events = new GameEventBus();
        var registry = new ActionRegistry(
            state,
            events,
            new GameRandom(1),
            new ActionGuardRegistry());

        registry.Register(new GameActionDefinition
        {
            Id = "test.targeted",
            Label = "Help Anna",
            Description = "Test action",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            IsAvailable = context => context.Target.Tags.Has("eligible"),
            Execute = _ => new GameActionResult(true)
        });

        var queued = registry.Execute("test.targeted", actor, target);
        Assert.True(queued.Success);
        Assert.Equal(target.Id, registry.GetQueuedActions(actor).Single().TargetId);

        target.Tags.Remove("eligible");
        var outcomes = registry.ExecuteQueued(YearPhase.LifeEvents);

        var outcome = Assert.Single(outcomes);
        Assert.Equal(QueuedActionResultCategory.Invalidated, outcome.Category);
        Assert.Equal(ActionReasonCodes.NoLongerEligible, outcome.ReasonCode);
        Assert.Equal(target.Id, outcome.TargetId);

        Assert.Empty(events.AllEvents);
    }

    [Fact]
    public void MissingQueuedTargetCannotDisappearSilently()
    {
        var state = new MutableGameState();
        var actor = state.CreatePerson("Jan", "Test", 30);
        var target = state.CreatePerson("Anna", "Test", 28);
        var registry = new ActionRegistry(
            state,
            new GameEventBus(),
            new GameRandom(2),
            new ActionGuardRegistry());

        registry.Register(new GameActionDefinition
        {
            Id = "test.targeted",
            Label = "Targeted",
            Description = "Test",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            IsAvailable = _ => true,
            Execute = _ => new GameActionResult(true)
        });

        Assert.True(registry.Execute("test.targeted", actor, target).Success);
        state.Remove(target.Id);

        var outcome = Assert.Single(
            registry.ExecuteQueued(YearPhase.LifeEvents));

        Assert.Equal(QueuedActionResultCategory.TargetMissing, outcome.Category);
        Assert.Equal(ActionReasonCodes.TargetMissing, outcome.ReasonCode);
    }

    [Fact]
    public void StructuredActionEvaluationCarriesContextResourcesAndMetadata()
    {
        var state = new MutableGameState();
        var actor = state.CreatePerson("Jan", "Test", 30);
        var target = state.CreatePerson("Anna", "Test", 28);
        var householdId = Guid.NewGuid();
        var registry = new ActionRegistry(
            state,
            new GameEventBus(),
            new GameRandom(3),
            new ActionGuardRegistry());

        registry.Register(new GameActionDefinition
        {
            Id = "test.structured_evaluation",
            Label = "Structured",
            Description = "Test structured action evaluation",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.Finances,
            IsAvailable = _ => throw new InvalidOperationException(
                "The structured evaluator should be authoritative."),
            EvaluateAvailability = _ => ActionEvaluationResult.Allowed(
                householdId,
                [
                    new ActionResourceRequirement(
                        "household.wealth",
                        1000m,
                        1500m,
                        "Household wealth",
                        "zł")
                ],
                new Dictionary<string, string>
                {
                    ["presentation.kind"] = "test"
                }),
            Execute = _ => new GameActionResult(true)
        });

        var evaluation = registry.Evaluate(
            "test.structured_evaluation",
            actor,
            target);

        Assert.True(evaluation.Available);
        Assert.Equal(ActionReasonCodes.Available, evaluation.ReasonCode);
        Assert.Equal("test.structured_evaluation", evaluation.ActionId);
        Assert.Equal(actor.Id, evaluation.ActorId);
        Assert.Equal(target.Id, evaluation.TargetId);
        Assert.Equal(householdId, evaluation.HouseholdId);
        Assert.Equal(ActionExecutionMode.Queued, evaluation.ExecutionMode);
        Assert.Equal(YearPhase.Finances, evaluation.QueuePhase);
        var requirement = Assert.Single(evaluation.ResourceRequirements);
        Assert.Equal("household.wealth", requirement.ResourceId);
        Assert.Equal(1000m, requirement.RequiredAmount);
        Assert.Equal(1500m, requirement.AvailableAmount);
        Assert.True(requirement.IsSatisfied);
        Assert.Equal("test", evaluation.PresentationMetadata["presentation.kind"]);
    }

    [Fact]
    public void UiQueueAndExecutionRevalidationUseTheSameStructuredRulePath()
    {
        var state = new MutableGameState();
        var actor = state.CreatePerson("Jan", "Test", 30);
        var target = state.CreatePerson("Anna", "Test", 28);
        target.Tags.Add("eligible");
        var evaluations = 0;
        var registry = new ActionRegistry(
            state,
            new GameEventBus(),
            new GameRandom(4),
            new ActionGuardRegistry());

        registry.Register(new GameActionDefinition
        {
            Id = "test.shared_rule_path",
            Label = "Shared Rule Path",
            Description = "Test shared evaluation path",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            IsAvailable = _ => throw new InvalidOperationException(
                "The structured evaluator should be authoritative."),
            EvaluateAvailability = context =>
            {
                evaluations++;
                return context.Target.Tags.Has("eligible")
                    ? ActionEvaluationResult.Allowed()
                    : ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "The target is no longer eligible.");
            },
            Execute = _ => new GameActionResult(true)
        });

        Assert.Contains(
            registry.GetAvailableActions(actor, target),
            action => action.Id == "test.shared_rule_path");

        var queued = registry.Execute(
            "test.shared_rule_path",
            actor,
            target);
        Assert.True(queued.Success);

        target.Tags.Remove("eligible");
        var outcome = Assert.Single(
            registry.ExecuteQueued(YearPhase.LifeEvents));

        Assert.Equal(3, evaluations);
        Assert.Equal(QueuedActionResultCategory.Invalidated, outcome.Category);
        Assert.Equal(ActionReasonCodes.NoLongerEligible, outcome.ReasonCode);
    }

    [Fact]
    public void StructuredReasonIsSharedByEvaluationAndQueueRejection()
    {
        var state = new MutableGameState();
        var actor = state.CreatePerson("Jan", "Test", 30);
        var registry = new ActionRegistry(
            state,
            new GameEventBus(),
            new GameRandom(5),
            new ActionGuardRegistry());

        registry.Register(new GameActionDefinition
        {
            Id = "test.insufficient_funds",
            Label = "Expensive",
            Description = "Test insufficient funds reason",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.Finances,
            IsAvailable = _ => false,
            EvaluateAvailability = _ => ActionEvaluationResult.Denied(
                ActionReasonCodes.InsufficientFunds,
                "The household cannot afford this action.",
                resourceRequirements:
                [
                    new ActionResourceRequirement(
                        "household.wealth",
                        2000m,
                        1000m,
                        "Household wealth",
                        "zł")
                ]),
            Execute = _ => new GameActionResult(true)
        });

        var evaluation = registry.Evaluate(
            "test.insufficient_funds",
            actor,
            actor);
        var execution = registry.Execute(
            "test.insufficient_funds",
            actor,
            actor);

        Assert.False(evaluation.Available);
        Assert.Equal(ActionReasonCodes.InsufficientFunds, evaluation.ReasonCode);
        Assert.False(evaluation.ResourceRequirements.Single().IsSatisfied);
        Assert.False(execution.Success);
        Assert.Equal(evaluation.ReasonCode, execution.ReasonCode);
        Assert.Empty(registry.GetQueuedActions(actor));
    }

    [Fact]
    public void AutonomousExecutionUsesExplicitContextWithoutSpoofingPlayableTag()
    {
        var state = new MutableGameState();
        var actor = state.CreatePerson("Jan", "Test", 30);
        var householdId = Guid.NewGuid();
        ActionExecutionOrigin? observedOrigin = null;
        Guid? observedHouseholdId = null;
        YearPhase? observedPhase = null;

        var registry = new ActionRegistry(
            state,
            new GameEventBus(),
            new GameRandom(6),
            new ActionGuardRegistry());

        registry.Register(new GameActionDefinition
        {
            Id = "test.autonomous_context",
            Label = "Autonomous Context",
            Description = "Test explicit autonomous execution context",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            IsAvailable = context => context.ActorHasControl,
            Execute = context =>
            {
                observedOrigin = context.Origin;
                observedHouseholdId = context.ActorHouseholdId;
                observedPhase = context.ExecutionPhase;
                return new GameActionResult(true);
            }
        });

        Assert.False(actor.Tags.Has("control.playable"));
        Assert.DoesNotContain(
            registry.GetAvailableActions(actor, actor),
            action => action.Id == "test.autonomous_context");
        Assert.Contains(
            registry.GetMechanicallyAvailableActions(actor, actor),
            action => action.Id == "test.autonomous_context");
        Assert.False(actor.Tags.Has("control.playable"));

        var autonomousAvailable = registry.GetAvailableActions(
            actor,
            actor,
            null,
            ActionExecutionContext.Autonomous(householdId));

        Assert.Contains(
            autonomousAvailable,
            action => action.Id == "test.autonomous_context");
        Assert.False(actor.Tags.Has("control.playable"));

        var queued = registry.ExecuteAutonomous(
            "test.autonomous_context",
            actor,
            actor,
            actorHouseholdId: householdId);

        Assert.True(queued.Success);
        var queuedInfo = Assert.Single(registry.GetQueuedActions(actor));
        Assert.Equal(ActionExecutionOrigin.Autonomous, queuedInfo.Origin);
        Assert.Equal(householdId, queuedInfo.ActorHouseholdId);
        Assert.False(actor.Tags.Has("control.playable"));

        var outcome = Assert.Single(registry.ExecuteQueued(YearPhase.LifeEvents));
        Assert.Equal(QueuedActionResultCategory.ExecutedSuccessfully, outcome.Category);
        Assert.Equal(ActionExecutionOrigin.Autonomous, observedOrigin.GetValueOrDefault());
        Assert.Equal(householdId, observedHouseholdId);
        Assert.Equal(YearPhase.LifeEvents, observedPhase.GetValueOrDefault());
        Assert.False(actor.Tags.Has("control.playable"));
    }

    [Fact]
    public void SystemExecutionUsesExplicitContextWithoutGrantingPlayerControl()
    {
        var state = new MutableGameState();
        var actor = state.CreatePerson("Jan", "Test", 30);
        ActionExecutionOrigin? observedOrigin = null;

        var registry = new ActionRegistry(
            state,
            new GameEventBus(),
            new GameRandom(7),
            new ActionGuardRegistry());

        registry.Register(new GameActionDefinition
        {
            Id = "test.system_context",
            Label = "System Context",
            Description = "Test explicit system execution context",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.Status,
            IsAvailable = context => context.ActorHasControl,
            Execute = context =>
            {
                observedOrigin = context.Origin;
                return new GameActionResult(true);
            }
        });

        Assert.True(registry.ExecuteSystem(
            "test.system_context",
            actor,
            actor,
            phase: YearPhase.Status).Success);
        Assert.False(actor.Tags.Has("control.playable"));

        var outcome = Assert.Single(registry.ExecuteQueued(YearPhase.Status));
        Assert.Equal(QueuedActionResultCategory.ExecutedSuccessfully, outcome.Category);
        Assert.Equal(ActionExecutionOrigin.System, observedOrigin.GetValueOrDefault());
        Assert.False(actor.Tags.Has("control.playable"));
    }

    private sealed class TestBoundary : IYearExecutionBoundary
    {
        private readonly IGameState _state;

        public TestBoundary(IGameState state)
        {
            _state = state;
        }

        public IYearExecutionCheckpoint Capture()
        {
            var year = _state.Year;
            var ages = _state.People.ToDictionary(person => person.Id, person => person.Age);
            return new Checkpoint(_state, year, ages);
        }

        private sealed class Checkpoint : IYearExecutionCheckpoint
        {
            private readonly IGameState _state;
            private readonly int _year;
            private readonly IReadOnlyDictionary<Guid, int> _ages;

            public Checkpoint(
                IGameState state,
                int year,
                IReadOnlyDictionary<Guid, int> ages)
            {
                _state = state;
                _year = year;
                _ages = ages;
            }

            public void Restore()
            {
                _state.Year = _year;
                foreach (var person in _state.People)
                {
                    if (_ages.TryGetValue(person.Id, out var age))
                        person.Age = age;
                }
            }
        }
    }

    private sealed class TestSystem : IYearSystem
    {
        private readonly Action<IGameState> _execute;

        public TestSystem(
            string id,
            YearPhase phase,
            Action<IGameState> execute,
            IReadOnlyCollection<string>? before = null,
            IReadOnlyCollection<string>? after = null)
        {
            Id = id;
            Phase = phase;
            _execute = execute;
            Before = before ?? Array.Empty<string>();
            After = after ?? Array.Empty<string>();
        }

        public string Id { get; }
        public YearPhase Phase { get; }
        public IReadOnlyCollection<string> Before { get; }
        public IReadOnlyCollection<string> After { get; }
        public void Execute(IGameState gameState) => _execute(gameState);
    }

    private sealed class MutableGameState : IGameState
    {
        private readonly List<IPerson> _people = [];

        public string DynastySurname { get; set; } = "Test";
        public int Year { get; set; } = GameCalendarConfiguration.GameStartYear;
        public int StartYear { get; set; } = GameCalendarConfiguration.GameStartYear;
        public IReadOnlyList<IPerson> People => _people;

        public IPerson CreatePerson(string name, string surname, int age, Guid? id = null)
        {
            var person = new Person(name, surname, age, id);
            _people.Add(person);
            return person;
        }

        public void ClearPeople() => _people.Clear();

        public void Remove(Guid id) =>
            _people.RemoveAll(person => person.Id == id);
    }
}
