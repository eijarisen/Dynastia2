namespace Dynastia.Contracts;

public interface IActionRegistry
{
    void Register(
        GameActionDefinition action);

    IReadOnlyList<GameActionDefinition> GetAvailableActions(
        IPerson actor,
        IPerson target);

    IReadOnlyList<GameActionDefinition>
        GetMechanicallyAvailableActions(
            IPerson actor,
            IPerson target);

    GameActionResult Execute(
        string actionId,
        IPerson actor,
        IPerson target);

    GameActionResult ExecuteAutonomous(
        string actionId,
        IPerson actor,
        IPerson target);

    IReadOnlyList<QueuedActionInfo> GetQueuedActions(
        IPerson actor);

    IReadOnlyList<QueuedActionInfo> GetAllQueuedActions();

    void CancelQueuedActions(
        IPerson actor);

    void ExecuteQueued(
        YearPhase phase);

    void RestoreQueuedActions(
        IReadOnlyList<QueuedActionInfo> queuedActions);

    string? GetBlockedReason(
        IPerson actor);
}
