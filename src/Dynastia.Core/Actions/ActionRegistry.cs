using Dynastia.Contracts;

namespace Dynastia.Core.Actions;

public sealed class ActionRegistry : IActionRegistry
{
    private readonly Dictionary<string, GameActionDefinition> _actions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<Func<IPerson, IPerson, IEnumerable<GameActionDefinition>>>
        _dynamicProviders = [];

    private readonly List<QueuedAction> _queued = [];
    private readonly List<QueuedActionOutcome> _lastQueuedOutcomes = [];

    private readonly IGameState _gameState;
    private readonly IGameEventBus _eventBus;
    private readonly IGameRandom _random;
    private readonly IActionGuardRegistry _guards;
    private readonly IStateReconciliationLifecycle? _reconciliation;

    public ActionRegistry(
        IGameState gameState,
        IGameEventBus eventBus,
        IGameRandom random,
        IActionGuardRegistry guards,
        IStateReconciliationLifecycle? reconciliation = null)
    {
        _gameState = gameState;
        _eventBus = eventBus;
        _random = random;
        _guards = guards;
        _reconciliation = reconciliation;
    }

    public IReadOnlyList<QueuedActionOutcome> LastQueuedActionOutcomes =>
        _lastQueuedOutcomes;

    public void Register(GameActionDefinition action)
    {
        ArgumentNullException.ThrowIfNull(action);

        EnsureActionCanBeEvaluated(action);

        if (!_actions.TryAdd(action.Id, action))
        {
            throw new InvalidOperationException(
                $"An action with ID '{action.Id}' is already registered.");
        }
    }

    public void RegisterDynamicProvider(
        Func<IPerson, IPerson, IEnumerable<GameActionDefinition>> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _dynamicProviders.Add(provider);
    }

    public IReadOnlyList<GameActionDefinition> GetAvailableActions(
        IPerson actor,
        IPerson target) =>
        GetAvailableActions(
            actor,
            target,
            null,
            ActionExecutionContext.Player);

    public IReadOnlyList<GameActionDefinition> GetAvailableActions(
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters) =>
        GetAvailableActions(
            actor,
            target,
            parameters,
            ActionExecutionContext.Player);

    public IReadOnlyList<GameActionDefinition> GetAvailableActions(
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters,
        ActionExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(executionContext);

        return GetActionCandidates(actor, target)
            .Where(action =>
                EvaluateDefinition(
                    action,
                    actor,
                    target,
                    parameters,
                    executionContext,
                    checkExistingQueue: true).Available)
            .OrderBy(action => action.Label)
            .ToList();
    }

    public ActionEvaluationResult Evaluate(
        string actionId,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters = null) =>
        Evaluate(
            actionId,
            actor,
            target,
            parameters,
            ActionExecutionContext.Player);

    public ActionEvaluationResult Evaluate(
        string actionId,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters,
        ActionExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(executionContext);

        var action = ResolveAction(actionId, actor, target);
        if (action is null)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.UnknownAction,
                $"Unknown action '{actionId}'.")
                with
                {
                    ActionId = actionId,
                    ActorId = actor.Id,
                    TargetId = target.Id
                };
        }

        return EvaluateDefinition(
            action,
            actor,
            target,
            parameters,
            executionContext,
            checkExistingQueue: true);
    }

    public IReadOnlyList<GameActionDefinition> GetMechanicallyAvailableActions(
        IPerson actor,
        IPerson target) =>
        GetAvailableActions(
            actor,
            target,
            null,
            ActionExecutionContext.Autonomous());

    public GameActionResult Execute(
        string actionId,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        var action = ResolveAction(actionId, actor, target);
        if (action is null)
        {
            return new GameActionResult(
                false,
                $"Unknown action '{actionId}'.",
                ActionReasonCodes.UnknownAction);
        }

        var evaluation = EvaluateDefinition(
            action,
            actor,
            target,
            parameters,
            ActionExecutionContext.Player,
            checkExistingQueue: true);

        if (!evaluation.Available)
        {
            return new GameActionResult(
                false,
                evaluation.Reason,
                evaluation.ReasonCode);
        }

        _queued.Add(CreateQueuedAction(
            action,
            actor,
            target,
            parameters,
            ActionExecutionContext.Player));

        return new GameActionResult(
            true,
            $"{action.Label} queued.",
            ActionReasonCodes.Available);
    }

    public GameActionResult ExecuteAutonomous(
        string actionId,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters = null,
        Guid? actorHouseholdId = null) =>
        ExecuteWithContext(
            actionId,
            actor,
            target,
            parameters,
            ActionExecutionContext.Autonomous(actorHouseholdId),
            "selected autonomously");

    public GameActionResult ExecuteSystem(
        string actionId,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters = null,
        Guid? actorHouseholdId = null,
        YearPhase? phase = null) =>
        ExecuteWithContext(
            actionId,
            actor,
            target,
            parameters,
            ActionExecutionContext.System(actorHouseholdId, phase),
            "selected by the simulation");

    private GameActionResult ExecuteWithContext(
        string actionId,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters,
        ActionExecutionContext executionContext,
        string queuedMessage)
    {
        var action = ResolveAction(actionId, actor, target);
        if (action is null)
        {
            return new GameActionResult(
                false,
                $"Unknown action '{actionId}'.",
                ActionReasonCodes.UnknownAction);
        }

        var evaluation = EvaluateDefinition(
            action,
            actor,
            target,
            parameters,
            executionContext,
            checkExistingQueue: true);

        if (!evaluation.Available)
        {
            return new GameActionResult(
                false,
                evaluation.Reason,
                evaluation.ReasonCode);
        }

        _queued.Add(CreateQueuedAction(
            action,
            actor,
            target,
            parameters,
            executionContext));

        return new GameActionResult(
            true,
            $"{action.Label} {queuedMessage}.",
            ActionReasonCodes.Available);
    }

    public IReadOnlyList<QueuedActionInfo> GetQueuedActions(IPerson actor) =>
        _queued
            .Where(queued => queued.ActorId == actor.Id)
            .Select(ToInfo)
            .ToList();

    public IReadOnlyList<QueuedActionInfo> GetAllQueuedActions() =>
        _queued
            .Select(ToInfo)
            .ToList();

    public void CancelQueuedActions(IPerson actor)
    {
        _queued.RemoveAll(queued => queued.ActorId == actor.Id);
    }

    public void RestoreQueuedActions(
        IReadOnlyList<QueuedActionInfo> queuedActions)
    {
        ArgumentNullException.ThrowIfNull(queuedActions);

        var restored = new List<QueuedAction>();
        var actors = new HashSet<Guid>();

        foreach (var saved in queuedActions)
        {
            if (!actors.Add(saved.ActorId))
            {
                throw new InvalidDataException(
                    "Save file contains more than one queued action for actor " +
                    $"{saved.ActorId}.");
            }

            var actor = _gameState.People.FirstOrDefault(person => person.Id == saved.ActorId)
                ?? throw new InvalidDataException(
                    $"Queued action '{saved.ActionId}' references a missing actor.");
            var target = _gameState.People.FirstOrDefault(person => person.Id == saved.TargetId)
                ?? throw new InvalidDataException(
                    $"Queued action '{saved.ActionId}' references a missing target.");

            var definition = ResolveAction(saved.ActionId, actor, target)
                ?? throw new InvalidDataException(
                    $"Save file references unknown action '{saved.ActionId}'.");

            var restoredParameters =
                new Dictionary<string, string>(
                    CloneParameters(saved.Parameters),
                    StringComparer.OrdinalIgnoreCase)
                {
                    [ActionCompatibilityParameters.RestoredQueuedAction] =
                        bool.TrueString
                };

            restored.Add(
                new QueuedAction(
                    definition.Id,
                    saved.ActorId,
                    saved.TargetId,
                    ResolveQueuePhase(definition),
                    string.IsNullOrWhiteSpace(saved.Label)
                        ? definition.Label
                        : saved.Label,
                    saved.Description ?? definition.Description,
                    restoredParameters,
                    saved.Origin,
                    saved.ActorHouseholdId));
        }

        _queued.Clear();
        _queued.AddRange(restored);
        _lastQueuedOutcomes.Clear();
    }

    public IReadOnlyList<QueuedActionOutcome> ExecuteQueued(YearPhase phase)
    {
        var pending = _queued
            .Where(queued => queued.Phase == phase)
            .ToList();

        _queued.RemoveAll(queued => queued.Phase == phase);
        _lastQueuedOutcomes.Clear();

        foreach (var queued in pending)
        {
            var outcome = ExecuteQueuedAction(queued);
            _lastQueuedOutcomes.Add(outcome);

            if (outcome.Category is
                QueuedActionResultCategory.Invalidated
                or QueuedActionResultCategory.ActorMissing
                or QueuedActionResultCategory.TargetMissing
                or QueuedActionResultCategory.ActionMissing
                or QueuedActionResultCategory.ActorBlocked)
            {
                PublishInvalidatedOutcome(outcome);
            }
        }

        return _lastQueuedOutcomes.ToList();
    }

    public string? GetBlockedReason(IPerson actor)
    {
        var result = _guards.Evaluate(actor);
        return result.Allowed ? null : result.Reason;
    }

    private QueuedActionOutcome ExecuteQueuedAction(QueuedAction queued)
    {
        var actor = _gameState.People.FirstOrDefault(person => person.Id == queued.ActorId);
        if (actor is null)
        {
            return Outcome(
                queued,
                QueuedActionResultCategory.ActorMissing,
                ActionReasonCodes.ActorMissing,
                "The acting person no longer exists.");
        }

        var target = _gameState.People.FirstOrDefault(person => person.Id == queued.TargetId);
        if (target is null)
        {
            return Outcome(
                queued,
                QueuedActionResultCategory.TargetMissing,
                ActionReasonCodes.TargetMissing,
                "The intended target no longer exists.");
        }

        var action = ResolveAction(queued.ActionId, actor, target);
        if (action is null)
        {
            return Outcome(
                queued,
                QueuedActionResultCategory.ActionMissing,
                ActionReasonCodes.UnknownAction,
                "The action is no longer registered.");
        }

        var guardResult = _guards.Evaluate(actor);
        if (!guardResult.Allowed && !action.BypassGuards)
        {
            return Outcome(
                queued,
                QueuedActionResultCategory.ActorBlocked,
                ActionReasonCodes.ActorBlocked,
                guardResult.Reason ?? "The actor can no longer perform this action.");
        }

        var executionContext = new ActionExecutionContext(
            queued.Origin,
            queued.ActorHouseholdId,
            queued.Phase);

        var evaluation = EvaluateDefinition(
            action,
            actor,
            target,
            queued.Parameters,
            executionContext,
            checkExistingQueue: false);

        if (!evaluation.Available)
        {
            return Outcome(
                queued,
                QueuedActionResultCategory.Invalidated,
                evaluation.ReasonCode,
                evaluation.Reason ?? "The action is no longer available.");
        }

        var result = action.Execute(
            CreateContext(
                actor,
                target,
                queued.Parameters,
                executionContext));

        if (result.Success)
        {
            _reconciliation?.Reconcile(
                action.Mode == ActionExecutionMode.Immediate
                    ? ReconciliationLifecycleStage.AfterImmediateAction
                    : ReconciliationLifecycleStage.AfterQueuedAction);
        }

        return Outcome(
            queued,
            result.Success
                ? QueuedActionResultCategory.ExecutedSuccessfully
                : QueuedActionResultCategory.ExecutedWithFailure,
            result.ReasonCode
                ?? (result.Success
                    ? ActionReasonCodes.Executed
                    : ActionReasonCodes.MechanicFailure),
            result.Message);
    }

    private void PublishInvalidatedOutcome(QueuedActionOutcome outcome)
    {
        var actor = _gameState.People.FirstOrDefault(person => person.Id == outcome.ActorId);
        var target = _gameState.People.FirstOrDefault(person => person.Id == outcome.TargetId);
        var actorName = actor?.Name ?? "The acting family member";
        var targetText = target is null || target.Id == outcome.ActorId
            ? string.Empty
            : $" for {target.Name}";

        var text =
            $"{actorName}'s queued action “{outcome.Label}”{targetText} " +
            $"could not be completed: {outcome.Message ?? "circumstances changed"}";

        _eventBus.Publish(
            new GameEvent
            {
                Type = "action.invalidated",
                Year = _gameState.Year,
                SubjectId = actor?.Id,
                RelatedPersonIds = target is null || target.Id == actor?.Id
                    ? Array.Empty<Guid>()
                    : new[] { target.Id },
                Data = new Dictionary<string, string>
                {
                    ["actionId"] = outcome.ActionId,
                    ["reasonCode"] = outcome.ReasonCode,
                    ["resultCategory"] = outcome.Category.ToString(),
                    ["text"] = text
                }
            });
    }

    private ActionEvaluationResult EvaluateDefinition(
        GameActionDefinition action,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters,
        ActionExecutionContext executionContext,
        bool checkExistingQueue)
    {
        var guardResult = _guards.Evaluate(actor);
        if (!guardResult.Allowed && !action.BypassGuards)
        {
            return CompleteEvaluation(
                ActionEvaluationResult.Denied(
                    ActionReasonCodes.ActorBlocked,
                    guardResult.Reason ?? "This character cannot perform actions."),
                action,
                actor,
                target);
        }

        if (checkExistingQueue
            && _queued.Any(queued => queued.ActorId == actor.Id))
        {
            return CompleteEvaluation(
                ActionEvaluationResult.Denied(
                    ActionReasonCodes.AlreadyQueued,
                    "This character already has a queued action."),
                action,
                actor,
                target);
        }

        var context = CreateContext(
            actor,
            target,
            parameters,
            executionContext);
        var evaluation = action.EvaluateAvailability is not null
            ? action.EvaluateAvailability(context)
            : action.IsAvailable!(context)
                ? ActionEvaluationResult.Allowed()
                : ActionEvaluationResult.Denied(
                    ActionReasonCodes.NoLongerEligible,
                    "This action is no longer available because circumstances changed.");

        if (evaluation.Available
            && !string.Equals(
                evaluation.ReasonCode,
                ActionReasonCodes.Available,
                StringComparison.OrdinalIgnoreCase))
        {
            evaluation = evaluation with
            {
                ReasonCode = ActionReasonCodes.Available
            };
        }
        else if (!evaluation.Available
                 && string.IsNullOrWhiteSpace(evaluation.ReasonCode))
        {
            evaluation = evaluation with
            {
                ReasonCode = ActionReasonCodes.NoLongerEligible
            };
        }

        return CompleteEvaluation(
            evaluation,
            action,
            actor,
            target);
    }

    private static ActionEvaluationResult CompleteEvaluation(
        ActionEvaluationResult evaluation,
        GameActionDefinition action,
        IPerson actor,
        IPerson target) =>
        evaluation with
        {
            ActionId = action.Id,
            ActorId = actor.Id,
            TargetId = target.Id,
            ExecutionMode = action.Mode,
            QueuePhase = ResolveQueuePhase(action)
        };

    private IReadOnlyList<GameActionDefinition> GetActionCandidates(
        IPerson actor,
        IPerson target)
    {
        var result = new Dictionary<string, GameActionDefinition>(
            _actions,
            StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _dynamicProviders)
        {
            foreach (var action in provider(actor, target) ?? Array.Empty<GameActionDefinition>())
            {
                EnsureActionCanBeEvaluated(action);

                if (result.ContainsKey(action.Id))
                {
                    throw new InvalidOperationException(
                        $"Dynamic action provider produced duplicate action ID '{action.Id}'.");
                }

                result[action.Id] = action;
            }
        }

        return result.Values.ToList();
    }

    private GameActionDefinition? ResolveAction(
        string actionId,
        IPerson actor,
        IPerson target)
    {
        if (_actions.TryGetValue(actionId, out var action))
            return action;

        foreach (var provider in _dynamicProviders)
        {
            var resolved = (provider(actor, target) ?? Array.Empty<GameActionDefinition>())
                .FirstOrDefault(candidate => candidate.Id.Equals(
                    actionId,
                    StringComparison.OrdinalIgnoreCase));
            if (resolved is not null)
            {
                EnsureActionCanBeEvaluated(resolved);
                return resolved;
            }
        }

        return null;
    }

    private static void EnsureActionCanBeEvaluated(
        GameActionDefinition action)
    {
        if (action.EvaluateAvailability is null
            && action.IsAvailable is null)
        {
            throw new InvalidOperationException(
                $"Action '{action.Id}' must provide an availability evaluator.");
        }
    }

    private static YearPhase ResolveQueuePhase(GameActionDefinition action) =>
        action.Mode == ActionExecutionMode.Immediate
            ? YearPhase.QueuedActionsEarly
            : action.QueuePhase;

    private GameActionContext CreateContext(
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters,
        ActionExecutionContext executionContext) =>
        new(
            _gameState,
            actor,
            target,
            _eventBus,
            _random,
            parameters,
            executionContext);

    private QueuedAction CreateQueuedAction(
        GameActionDefinition action,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters,
        ActionExecutionContext executionContext) =>
        new(
            action.Id,
            actor.Id,
            target.Id,
            ResolveQueuePhase(action),
            action.Label,
            action.Description,
            CloneParameters(parameters),
            executionContext.Origin,
            executionContext.ActorHouseholdId);

    private QueuedActionInfo ToInfo(QueuedAction queued) =>
        new(
            queued.ActionId,
            queued.Label,
            queued.Phase,
            queued.ActorId,
            queued.TargetId,
            queued.Description,
            queued.Parameters,
            queued.Origin,
            queued.ActorHouseholdId);

    private static QueuedActionOutcome Outcome(
        QueuedAction queued,
        QueuedActionResultCategory category,
        string reasonCode,
        string? message) =>
        new(
            queued.ActionId,
            queued.ActorId,
            queued.TargetId,
            queued.Phase,
            category,
            reasonCode,
            queued.Label,
            message,
            queued.Parameters);

    private static IReadOnlyDictionary<string, string> CloneParameters(
        IReadOnlyDictionary<string, string>? parameters) =>
        parameters is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(
                parameters,
                StringComparer.OrdinalIgnoreCase);

    private sealed record QueuedAction(
        string ActionId,
        Guid ActorId,
        Guid TargetId,
        YearPhase Phase,
        string Label,
        string? Description,
        IReadOnlyDictionary<string, string> Parameters,
        ActionExecutionOrigin Origin,
        Guid? ActorHouseholdId);
}
