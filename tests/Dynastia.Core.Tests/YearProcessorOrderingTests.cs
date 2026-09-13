using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.Core.Tests;

public sealed class YearProcessorOrderingTests
{
    [Fact]
    public void SamePhaseBeforeAfterOverridesAlphabeticalOrder()
    {
        var executed = new List<string>();
        var state = new GameState();
        var registry = new YearSystemRegistry();

        registry.Register(
            new TestSystem(
                "a.second",
                YearPhase.LifeEvents,
                executed));

        registry.Register(
            new TestSystem(
                "z.first",
                YearPhase.LifeEvents,
                executed,
                before: ["a.second"]));

        new YearProcessor(
            state,
            registry)
            .AdvanceYear();

        Assert.Equal(
            ["z.first", "a.second"],
            executed);
    }

    [Fact]
    public void SamePhaseDependencyCycleThrows()
    {
        var state = new GameState();
        var registry = new YearSystemRegistry();

        registry.Register(
            new TestSystem(
                "one",
                YearPhase.LifeEvents,
                [],
                after: ["two"]));

        registry.Register(
            new TestSystem(
                "two",
                YearPhase.LifeEvents,
                [],
                after: ["one"]));

        Assert.Throws<InvalidOperationException>(
            () =>
                new YearProcessor(
                    state,
                    registry)
                    .AdvanceYear());
    }


    [Fact]
    public void MarriageRepairAndLateMortalityHaveExplicitOrdering()
    {
        Assert.True(YearPhase.LifeEvents < YearPhase.MarriageEvaluation);
        Assert.True(YearPhase.MarriageEvaluation < YearPhase.MarriageRepair);
        Assert.True(YearPhase.MarriageRepair < YearPhase.MarriageDivorce);
        Assert.True(YearPhase.MarriageDivorce < YearPhase.LateMortality);
        Assert.True(YearPhase.LateMortality < YearPhase.Inheritance);
    }

    private sealed class TestSystem : IYearSystem
    {
        private readonly IList<string> _executed;

        public TestSystem(
            string id,
            YearPhase phase,
            IList<string> executed,
            IReadOnlyCollection<string>? before = null,
            IReadOnlyCollection<string>? after = null)
        {
            Id = id;
            Phase = phase;
            _executed = executed;
            Before = before ?? Array.Empty<string>();
            After = after ?? Array.Empty<string>();
        }

        public string Id { get; }

        public YearPhase Phase { get; }

        public IReadOnlyCollection<string> Before { get; }

        public IReadOnlyCollection<string> After { get; }

        public void Execute(
            IGameState gameState)
        {
            _executed.Add(Id);
        }
    }
}
