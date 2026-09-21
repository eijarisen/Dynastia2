namespace Dynastia.Contracts;

public interface IActionRegistry
{
    IReadOnlyList<QueuedActionOutcome> LastQueuedActionOutcomes { get; }

    void Register(
        GameActionDefinition action);

    void RegisterDynamicProvider(
        Func<IPerson, IPerson, IEnumerable<GameActionDefinition>> provider);

    IReadOnlyList<GameActionDefinition> GetCandidateActions(
        IPerson actor,
        IPerson target);

    IReadOnlyList<GameActionDefinition> GetAvailableActions(
        IPerson actor,
        IPerson target);

    ActionEvaluationResult Evaluate(
        string actionId,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters = null);

    ActionEvaluationResult Evaluate(
        string actionId,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters,
        ActionExecutionContext executionContext);

    IReadOnlyList<GameActionDefinition> GetAvailableActions(
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters);

    IReadOnlyList<GameActionDefinition> GetAvailableActions(
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters,
        ActionExecutionContext executionContext);

    IReadOnlyList<GameActionDefinition>
        GetMechanicallyAvailableActions(
            IPerson actor,
            IPerson target);

    GameActionResult Execute(
        string actionId,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters = null);

    GameActionResult ExecuteAutonomous(
        string actionId,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters = null,
        Guid? actorHouseholdId = null);

    GameActionResult ExecuteSystem(
        string actionId,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters = null,
        Guid? actorHouseholdId = null,
        YearPhase? phase = null);

    IReadOnlyList<QueuedActionInfo> GetQueuedActions(
        IPerson actor);

    IReadOnlyList<QueuedActionInfo> GetAllQueuedActions();

    void CancelQueuedActions(
        IPerson actor);

    IReadOnlyList<QueuedActionOutcome> ExecuteQueued(
        YearPhase phase);

    void RestoreQueuedActions(
        IReadOnlyList<QueuedActionInfo> queuedActions);

    string? GetBlockedReason(
        IPerson actor);
}
