using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.Core.Tests;

[CollectionDefinition("ConsoleOutput", DisableParallelization = true)]
public sealed class ConsoleOutputCollectionDefinition
{
}

[Collection("ConsoleOutput")]
public sealed class YearProcessorTimingTests
{
    [Fact]
    public void AdvanceYearLogsElapsedMillisecondsForEachSystem()
    {
        var state = new GameState();
        var registry = new YearSystemRegistry();
        registry.Register(new TestSystem("timing.test"));

        var expectedYear = state.Year + 1;
        var output = CaptureConsole(
            () => new YearProcessor(state, registry).AdvanceYear());

        Assert.Contains($"[{expectedYear}] Running timing.test", output);
        Assert.Matches(
            $@"\[{expectedYear}\] Finished timing\.test in \d+(?:[.,]\d+)? ms",
            output);
    }

    [Fact]
    public void OptInProfilerLogsYearExecutionBoundaries()
    {
        var previous =
            Environment.GetEnvironmentVariable(
                YearPerformanceProfiler.EnvironmentVariableName);

        try
        {
            Environment.SetEnvironmentVariable(
                YearPerformanceProfiler.EnvironmentVariableName,
                "1");

            var state = new GameState();
            var registry = new YearSystemRegistry();
            registry.Register(new TestSystem("timing.profile"));

            var expectedYear = state.Year + 1;
            var output = CaptureConsole(
                () => new YearProcessor(state, registry).AdvanceYear());

            Assert.Contains($"[PERF][{expectedYear}] year.graph_ordering ms=", output);
            Assert.Contains($"[PERF][{expectedYear}] year.checkpoint_capture ms=", output);
            Assert.Contains($"[PERF][{expectedYear}] year.turn_start_queue_capture ms=", output);
            Assert.Contains($"[PERF][{expectedYear}] year.reconcile_before ms=", output);
            Assert.Matches(
                $@"\[{expectedYear}\] Finished timing\.profile in \d+(?:[.,]\d+)? ms",
                output);
            Assert.Contains($"[PERF][{expectedYear}] year.reconcile_after ms=", output);
            Assert.Contains($"[PERF][{expectedYear}] year.advance_total ms=", output);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                YearPerformanceProfiler.EnvironmentVariableName,
                previous);
        }
    }

    [Fact]
    public void FailedSystemStillLogsElapsedMilliseconds()
    {
        var state = new GameState();
        var registry = new YearSystemRegistry();
        registry.Register(new TestSystem(
            "timing.failure",
            () => throw new InvalidOperationException("boom")));

        var expectedYear = state.Year + 1;
        var original = Console.Out;
        using var writer = new StringWriter();
        InvalidOperationException exception;

        try
        {
            Console.SetOut(writer);
            exception = Assert.Throws<InvalidOperationException>(
                () => new YearProcessor(state, registry).AdvanceYear());
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("timing.failure", exception.Message);
        Assert.Matches(
            $@"\[{expectedYear}\] Finished timing\.failure in \d+(?:[.,]\d+)? ms",
            writer.ToString());
    }

    private static string CaptureConsole(Action action)
    {
        var original = Console.Out;
        using var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);
            action();
        }
        finally
        {
            Console.SetOut(original);
        }

        return writer.ToString();
    }

    private sealed class TestSystem : IYearSystem
    {
        private readonly Action _execute;

        public TestSystem(
            string id,
            Action? execute = null)
        {
            Id = id;
            _execute = execute ?? (() => { });
        }

        public string Id { get; }

        public YearPhase Phase => YearPhase.LifeEvents;

        public IReadOnlyCollection<string> Before => Array.Empty<string>();

        public IReadOnlyCollection<string> After => Array.Empty<string>();

        public void Execute(IGameState gameState)
        {
            _execute();
        }
    }
}
