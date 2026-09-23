using Dynastia.Contracts;

namespace Dynastia.App.Tests;

internal sealed class RecordingActionRegistry(IActionRegistry inner) : IActionRegistry
{
    internal sealed record Submission(string ActionId, Guid ActorId, Guid TargetId,
        IReadOnlyDictionary<string, string>? Parameters);

    public List<Submission> Submissions { get; } = [];
    public IReadOnlyList<QueuedActionOutcome> LastQueuedActionOutcomes => inner.LastQueuedActionOutcomes;

    public void Register(GameActionDefinition action) =>
        inner.Register(action);

    public void RegisterDynamicProvider(Func<IPerson, IPerson, IEnumerable<GameActionDefinition>> provider) =>
        inner.RegisterDynamicProvider(provider);

    public IReadOnlyList<GameActionDefinition> GetCandidateActions(IPerson actor, IPerson target) =>
        inner.GetCandidateActions(actor, target);

    public IReadOnlyList<GameActionDefinition> GetAvailableActions(IPerson actor, IPerson target) =>
        inner.GetAvailableActions(actor, target);

    public ActionEvaluationResult Evaluate(string actionId, IPerson actor, IPerson target, IReadOnlyDictionary<string, string>? parameters = null) =>
        inner.Evaluate(actionId, actor, target, parameters);

    public ActionEvaluationResult Evaluate(string actionId, IPerson actor, IPerson target, IReadOnlyDictionary<string, string>? parameters, ActionExecutionContext executionContext) =>
        inner.Evaluate(actionId, actor, target, parameters, executionContext);

    public IReadOnlyList<GameActionDefinition> GetAvailableActions(IPerson actor, IPerson target, IReadOnlyDictionary<string, string>? parameters) =>
        inner.GetAvailableActions(actor, target, parameters);

    public IReadOnlyList<GameActionDefinition> GetAvailableActions(IPerson actor, IPerson target, IReadOnlyDictionary<string, string>? parameters, ActionExecutionContext executionContext) =>
        inner.GetAvailableActions(actor, target, parameters, executionContext);

    public IReadOnlyList<GameActionDefinition> GetMechanicallyAvailableActions(IPerson actor, IPerson target) =>
        inner.GetMechanicallyAvailableActions(actor, target);

    public GameActionResult Execute(string actionId, IPerson actor, IPerson target, IReadOnlyDictionary<string, string>? parameters = null)
    {
        Submissions.Add(new Submission(actionId, actor.Id, target.Id,
            parameters is null ? null : new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase)));
        return inner.Execute(actionId, actor, target, parameters);
    }

    public GameActionResult ExecuteAutonomous(string actionId, IPerson actor, IPerson target, IReadOnlyDictionary<string, string>? parameters = null, Guid? actorHouseholdId = null) =>
        inner.ExecuteAutonomous(actionId, actor, target, parameters, actorHouseholdId);

    public GameActionResult ExecuteSystem(string actionId, IPerson actor, IPerson target, IReadOnlyDictionary<string, string>? parameters = null, Guid? actorHouseholdId = null, YearPhase? phase = null) =>
        inner.ExecuteSystem(actionId, actor, target, parameters, actorHouseholdId, phase);

    public IReadOnlyList<QueuedActionInfo> GetQueuedActions(IPerson actor) =>
        inner.GetQueuedActions(actor);

    public IReadOnlyList<QueuedActionInfo> GetAllQueuedActions() =>
        inner.GetAllQueuedActions();

    public void CancelQueuedActions(IPerson actor) =>
        inner.CancelQueuedActions(actor);

    public void CaptureTurnStartQueuedActions() =>
        inner.CaptureTurnStartQueuedActions();

    public IReadOnlyList<QueuedActionOutcome> ExecuteTurnStartQueuedActions() =>
        inner.ExecuteTurnStartQueuedActions();

    public IReadOnlyList<QueuedActionOutcome> ExecuteQueued(YearPhase phase) =>
        inner.ExecuteQueued(phase);

    public void RestoreQueuedActions(IReadOnlyList<QueuedActionInfo> queuedActions) =>
        inner.RestoreQueuedActions(queuedActions);

    public string? GetBlockedReason(IPerson actor) =>
        inner.GetBlockedReason(actor);

}
