using System.Diagnostics;
using Dynastia.Contracts;

namespace Dynastia.Core.Simulation;

public sealed class YearProcessor
{
    private readonly IGameState _gameState;
    private readonly IYearSystemRegistry _registry;
    private readonly IYearExecutionBoundary? _boundary;
    private readonly IStateReconciliationLifecycle? _reconciliation;
    private readonly IActionRegistry? _actions;
    private long _orderedSystemsVersion = long.MinValue;
    private IReadOnlyList<IYearSystem>? _orderedSystems;

    public YearProcessor(
        IGameState gameState,
        IYearSystemRegistry registry,
        IYearExecutionBoundary? boundary = null,
        IStateReconciliationLifecycle? reconciliation = null,
        IActionRegistry? actions = null)
    {
        _gameState = gameState;
        _registry = registry;
        _boundary = boundary;
        _reconciliation = reconciliation;
        _actions = actions;
    }

    public void AdvanceYear()
    {
        var profileEnabled =
            YearPerformanceProfiler.IsEnabled;

        var targetYear =
            _gameState.Year + 1;

        var advanceStartedAt =
            profileEnabled
                ? YearPerformanceProfiler.StartTimestamp()
                : 0;

        // Validate and fully order the graph before mutating any game state.
        var orderingStartedAt =
            profileEnabled
                ? YearPerformanceProfiler.StartTimestamp()
                : 0;

        var systems = GetOrderedSystems();

        if (profileEnabled)
        {
            YearPerformanceProfiler.LogDuration(
                targetYear,
                "year.graph_ordering",
                orderingStartedAt,
                $"systems={systems.Count} order={string.Join(">", systems.Select(system => system.Id))}");
        }

        var checkpointStartedAt =
            profileEnabled
                ? YearPerformanceProfiler.StartTimestamp()
                : 0;

        var checkpoint = _boundary?.Capture();

        if (profileEnabled)
        {
            YearPerformanceProfiler.LogDuration(
                targetYear,
                "year.checkpoint_capture",
                checkpointStartedAt);
        }

        IYearSystem? currentSystem = null;

        try
        {
            var queueCaptureStartedAt =
                profileEnabled
                    ? YearPerformanceProfiler.StartTimestamp()
                    : 0;

            _actions?.CaptureTurnStartQueuedActions();

            if (profileEnabled)
            {
                YearPerformanceProfiler.LogDuration(
                    targetYear,
                    "year.turn_start_queue_capture",
                    queueCaptureStartedAt);
            }

            var beforeYearStartedAt =
                profileEnabled
                    ? YearPerformanceProfiler.StartTimestamp()
                    : 0;

            _reconciliation?.Reconcile(
                ReconciliationLifecycleStage.BeforeYear);

            if (profileEnabled)
            {
                YearPerformanceProfiler.LogDuration(
                    targetYear,
                    "year.reconcile_before",
                    beforeYearStartedAt);
            }

            _gameState.Year++;

            foreach (var system in systems)
            {
                currentSystem = system;

                Console.WriteLine(
                    $"[{_gameState.Year}] Running {system.Id}");

                var peopleCountBefore = _gameState.People.Count;
                var startedAt = Stopwatch.GetTimestamp();

                try
                {
                    system.Execute(_gameState);
                }
                finally
                {
                    var elapsed = Stopwatch.GetElapsedTime(startedAt);
                    Console.WriteLine(
                        $"[{_gameState.Year}] Finished {system.Id} in {elapsed.TotalMilliseconds:F3} ms");

                }

                if (_gameState.People.Count > peopleCountBefore)
                {
                    _reconciliation?.Reconcile(
                        ReconciliationLifecycleStage.AfterPersonCreated);
                }
            }

            var afterYearStartedAt =
                profileEnabled
                    ? YearPerformanceProfiler.StartTimestamp()
                    : 0;

            _reconciliation?.Reconcile(
                ReconciliationLifecycleStage.AfterYear);

            if (profileEnabled)
            {
                YearPerformanceProfiler.LogDuration(
                    _gameState.Year,
                    "year.reconcile_after",
                    afterYearStartedAt);
            }
        }
        catch (Exception exception)
        {
            if (checkpoint is not null)
            {
                try
                {
                    checkpoint.Restore();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        BuildFailureMessage(currentSystem),
                        exception,
                        rollbackException);
                }
            }

            throw new InvalidOperationException(
                BuildFailureMessage(currentSystem),
                exception);
        }
        finally
        {
            if (profileEnabled)
            {
                YearPerformanceProfiler.LogDuration(
                    targetYear,
                    "year.advance_total",
                    advanceStartedAt);
            }
        }
    }

    private IReadOnlyList<IYearSystem> GetOrderedSystems()
    {
        if (_registry is not IVersionedYearSystemRegistry versioned)
            return OrderSystems(_registry.Systems);

        var version = versioned.Version;
        if (_orderedSystems is not null
            && _orderedSystemsVersion == version)
        {
            return _orderedSystems;
        }

        var ordered = OrderSystems(_registry.Systems);
        _orderedSystems = ordered;
        _orderedSystemsVersion = version;
        return ordered;
    }

    internal static IReadOnlyList<IYearSystem> OrderSystems(
        IReadOnlyCollection<IYearSystem> systems)
    {
        var all = systems.ToList();

        var byId = all.ToDictionary(
            system => system.Id,
            StringComparer.OrdinalIgnoreCase);

        ValidateDependencies(all, byId);

        var result = new List<IYearSystem>();

        foreach (var phaseGroup in all
            .GroupBy(system => system.Phase)
            .OrderBy(group => group.Key))
        {
            result.AddRange(
                OrderPhase(
                    phaseGroup.ToList(),
                    byId));
        }

        return result;
    }

    private static IReadOnlyList<IYearSystem> OrderPhase(
        IReadOnlyList<IYearSystem> systems,
        IReadOnlyDictionary<string, IYearSystem> byId)
    {
        var phaseIds = systems
            .Select(system => system.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var outgoing = systems.ToDictionary(
            system => system.Id,
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        var indegree = systems.ToDictionary(
            system => system.Id,
            _ => 0,
            StringComparer.OrdinalIgnoreCase);

        void AddEdge(string beforeId, string afterId)
        {
            if (!phaseIds.Contains(beforeId)
                || !phaseIds.Contains(afterId))
            {
                return;
            }

            if (outgoing[beforeId].Add(afterId))
                indegree[afterId]++;
        }

        foreach (var system in systems)
        {
            foreach (var beforeId in system.Before)
                AddEdge(system.Id, beforeId);

            foreach (var afterId in system.After)
                AddEdge(afterId, system.Id);
        }

        var ready = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in indegree)
        {
            if (item.Value == 0)
                ready.Add(item.Key);
        }

        var result = new List<IYearSystem>();

        while (ready.Count > 0)
        {
            var id = ready.Min!;
            ready.Remove(id);
            result.Add(byId[id]);

            foreach (var targetId in outgoing[id]
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                indegree[targetId]--;
                if (indegree[targetId] == 0)
                    ready.Add(targetId);
            }
        }

        if (result.Count != systems.Count)
        {
            var cycleIds = indegree
                .Where(pair => pair.Value > 0)
                .Select(pair => pair.Key)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

            throw new InvalidOperationException(
                "Year-system dependency cycle detected " +
                $"in phase {systems[0].Phase}: " +
                string.Join(", ", cycleIds));
        }

        return result;
    }

    private static void ValidateDependencies(
        IReadOnlyList<IYearSystem> systems,
        IReadOnlyDictionary<string, IYearSystem> byId)
    {
        foreach (var system in systems)
        {
            foreach (var beforeId in system.Before)
            {
                if (!byId.TryGetValue(beforeId, out var target))
                {
                    throw new InvalidOperationException(
                        $"Year system '{system.Id}' requires missing dependency " +
                        $"'{beforeId}' in its Before list.");
                }

                if (target.Phase < system.Phase)
                {
                    throw new InvalidOperationException(
                        $"Year system '{system.Id}' declares Before '{target.Id}', " +
                        $"but phase {system.Phase} runs after {target.Phase}.");
                }
            }

            foreach (var afterId in system.After)
            {
                if (!byId.TryGetValue(afterId, out var target))
                {
                    throw new InvalidOperationException(
                        $"Year system '{system.Id}' requires missing dependency " +
                        $"'{afterId}' in its After list.");
                }

                if (target.Phase > system.Phase)
                {
                    throw new InvalidOperationException(
                        $"Year system '{system.Id}' declares After '{target.Id}', " +
                        $"but phase {system.Phase} runs before {target.Phase}.");
                }
            }
        }
    }

    private static string BuildFailureMessage(IYearSystem? system) =>
        system is null
            ? "Year processing failed before a year system completed. The pre-year state was restored."
            : $"Year processing failed in system '{system.Id}' ({system.Phase}). " +
              "The pre-year state was restored.";
}
