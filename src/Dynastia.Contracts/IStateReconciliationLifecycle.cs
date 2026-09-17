namespace Dynastia.Contracts;

public interface IStateReconciliationLifecycle
{
    void Register(
        string id,
        IReadOnlyCollection<ReconciliationLifecycleStage> stages,
        Action<ReconciliationLifecycleStage> reconcile,
        int order = 0);

    void Reconcile(
        ReconciliationLifecycleStage stage);
}
