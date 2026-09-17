using Dynastia.Contracts;

namespace Dynastia.Core.Simulation;

public sealed class StateReconciliationLifecycle :
    IStateReconciliationLifecycle
{
    private readonly List<Registration> _registrations = [];
    private bool _running;

    public void Register(
        string id,
        IReadOnlyCollection<ReconciliationLifecycleStage> stages,
        Action<ReconciliationLifecycleStage> reconcile,
        int order = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(reconcile);

        if (_registrations.Any(existing =>
            existing.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"A reconciliation participant with ID '{id}' is already registered.");
        }

        _registrations.Add(
            new Registration(
                id,
                stages.ToHashSet(),
                reconcile,
                order));
    }

    public void Reconcile(
        ReconciliationLifecycleStage stage)
    {
        if (_running)
            return;

        _running = true;

        try
        {
            foreach (var registration in _registrations
                .Where(candidate => candidate.Stages.Contains(stage))
                .OrderBy(candidate => candidate.Order)
                .ThenBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    registration.Reconcile(stage);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"State reconciliation '{registration.Id}' failed during {stage}.",
                        exception);
                }
            }
        }
        finally
        {
            _running = false;
        }
    }

    private sealed record Registration(
        string Id,
        IReadOnlySet<ReconciliationLifecycleStage> Stages,
        Action<ReconciliationLifecycleStage> Reconcile,
        int Order);
}
