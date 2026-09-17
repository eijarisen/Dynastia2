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

    // Autonomous actions still use the historical temporary playable-tag
    // compatibility path. Batch 4 replaces that with an explicit execution
    // context; keeping it here prevents this corrective batch from changing
    // autonomous action eligibility.
    private readonly HashSet<AutonomousQueuedAction> _autonomousQueued = [];

    private readonly IGameState _gameState;
    private readonly IGameEventBus _eventBus;
    private readonly IGameRandom _random;
    private readonly IActionGuardRegistry _guards;

    public ActionRegistry(
        IGameState gameState,
        IGameEventBus eventBus,
        IGameRandom random,
        IActionGuardRegistry guards)
    {
        _gameState = gameState;
        _eventBus = eventBus;
        _random = random;
        _guards = guards;
    }

    public IReadOnlyList<QueuedActionOutcome> LastQueuedActionOutcomes =>
        _lastQueuedOutcomes;

    public void Register(GameActionDefinition action)
    {
        ArgumentNullException.ThrowIfNull(action);

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
        GetAvailableActions(actor, target, null);

    public IReadOnlyList<GameActionDefinition> GetAvailableActions(
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters)
    {
        if (_queued.Any(queued => queued.ActorId == actor.Id))
            return [];

        return GetActionCandidates(actor, target)
            .Where(action =>
                EvaluateDefinition(
                    action,
                    actor,
                    target,
                    parameters,
                    checkExistingQueue: false).Available)
            .OrderBy(action => action.Label)
            .ToList();
    }

    public ActionEvaluationResult Evaluate(
        string actionId,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        var action = ResolveAction(actionId, actor, target);
        if (action is null)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.UnknownAction,
                $"Unknown action '{actionId}'.");
        }

        return EvaluateDefinition(
            action,
            actor,
            target,
            parameters,
            checkExistingQueue: true);
    }

    public IReadOnlyList<GameActionDefinition> GetMechanicallyAvailableActions(
        IPerson actor,
        IPerson target)
    {
        var hadPlayableTag = actor.Tags.Has("control.playable");
        if (!hadPlayableTag)
            actor.Tags.Add("control.playable");

        try
        {
            return GetAvailableActions(actor, target);
        }
        finally
        {
            if (!hadPlayableTag)
                actor.Tags.Remove("control.playable");
        }
    }

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
            checkExistingQueue: true);

        if (!evaluation.Available)
        {
            return new GameActionResult(
                false,
                evaluation.Reason,
                evaluation.ReasonCode);
        }

        _queued.Add(CreateQueuedAction(action, actor, target, parameters));

        return new GameActionResult(
            true,
            $"{action.Label} queued.",
            ActionReasonCodes.Available);
    }

    public GameActionResult ExecuteAutonomous(
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

        if (_queued.Any(queued => queued.ActorId == actor.Id))
        {
            return new GameActionResult(
                false,
                "This character already has a queued action.",
                ActionReasonCodes.AlreadyQueued);
        }

        var guardResult = _guards.Evaluate(actor);
        if (!guardResult.Allowed && !action.BypassGuards)
        {
            return new GameActionResult(
                false,
                guardResult.Reason ?? "This character cannot perform actions.",
                ActionReasonCodes.ActorBlocked);
        }

        var hadPlayableTag = actor.Tags.Has("control.playable");
        if (!hadPlayableTag)
            actor.Tags.Add("control.playable");

        try
        {
            var evaluation = EvaluateDefinition(
                action,
                actor,
                target,
                parameters,
                checkExistingQueue: false);

            if (!evaluation.Available)
            {
                return new GameActionResult(
                    false,
                    evaluation.Reason,
                    evaluation.ReasonCode);
            }

            var queued = CreateQueuedAction(action, actor, target, parameters);
            _queued.Add(queued);
            _autonomousQueued.Add(
                new AutonomousQueuedAction(
                    queued.ActionId,
                    queued.ActorId,
                    queued.TargetId));

            return new GameActionResult(
                true,
                $"{action.Label} selected autonomously.",
                ActionReasonCodes.Available);
        }
        finally
        {
            if (!hadPlayableTag)
                actor.Tags.Remove("control.playable");
        }
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
        _autonomousQueued.RemoveWhere(queued => queued.ActorId == actor.Id);
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
                    restoredParameters));
        }

        _queued.Clear();
        _queued.AddRange(restored);
        _autonomousQueued.Clear();
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
            _autonomousQueued.Remove(
                new AutonomousQueuedAction(queued.ActionId, queued.ActorId, queued.TargetId));
            return Outcome(
                queued,
                QueuedActionResultCategory.ActorMissing,
                ActionReasonCodes.ActorMissing,
                "The acting person no longer exists.");
        }

        var target = _gameState.People.FirstOrDefault(person => person.Id == queued.TargetId);
        if (target is null)
        {
            _autonomousQueued.Remove(
                new AutonomousQueuedAction(queued.ActionId, queued.ActorId, queued.TargetId));
            return Outcome(
                queued,
                QueuedActionResultCategory.TargetMissing,
                ActionReasonCodes.TargetMissing,
                "The intended target no longer exists.");
        }

        var action = ResolveAction(queued.ActionId, actor, target);
        if (action is null)
        {
            _autonomousQueued.Remove(
                new AutonomousQueuedAction(queued.ActionId, queued.ActorId, queued.TargetId));
            return Outcome(
                queued,
                QueuedActionResultCategory.ActionMissing,
                ActionReasonCodes.UnknownAction,
                "The action is no longer registered.");
        }

        var guardResult = _guards.Evaluate(actor);
        if (!guardResult.Allowed && !action.BypassGuards)
        {
            _autonomousQueued.Remove(
                new AutonomousQueuedAction(queued.ActionId, queued.ActorId, queued.TargetId));
            return Outcome(
                queued,
                QueuedActionResultCategory.ActorBlocked,
                ActionReasonCodes.ActorBlocked,
                guardResult.Reason ?? "The actor can no longer perform this action.");
        }

        var autonomousKey =
            new AutonomousQueuedAction(queued.ActionId, queued.ActorId, queued.TargetId);
        var isAutonomous = _autonomousQueued.Remove(autonomousKey);
        var hadPlayableTag = actor.Tags.Has("control.playable");

        if (isAutonomous && !hadPlayableTag)
            actor.Tags.Add("control.playable");

        try
        {
            var evaluation = EvaluateDefinition(
                action,
                actor,
                target,
                queued.Parameters,
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
                CreateContext(actor, target, queued.Parameters));

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
        finally
        {
            if (isAutonomous && !hadPlayableTag)
                actor.Tags.Remove("control.playable");
        }
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
        bool checkExistingQueue)
    {
        var guardResult = _guards.Evaluate(actor);
        if (!guardResult.Allowed && !action.BypassGuards)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.ActorBlocked,
                guardResult.Reason ?? "This character cannot perform actions.");
        }

        if (checkExistingQueue
            && _queued.Any(queued => queued.ActorId == actor.Id))
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.AlreadyQueued,
                "This character already has a queued action.");
        }

        var context = CreateContext(actor, target, parameters);
        if (!action.IsAvailable(context))
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                "This action is no longer available because circumstances changed.");
        }

        return ActionEvaluationResult.Allowed();
    }

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
                return resolved;
        }

        return null;
    }

    private static YearPhase ResolveQueuePhase(GameActionDefinition action) =>
        action.Mode == ActionExecutionMode.Immediate
            ? YearPhase.QueuedActionsEarly
            : action.QueuePhase;

    private GameActionContext CreateContext(
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters = null) =>
        new(
            _gameState,
            actor,
            target,
            _eventBus,
            _random,
            parameters);

    private QueuedAction CreateQueuedAction(
        GameActionDefinition action,
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string>? parameters) =>
        new(
            action.Id,
            actor.Id,
            target.Id,
            ResolveQueuePhase(action),
            action.Label,
            action.Description,
            CloneParameters(parameters));

    private QueuedActionInfo ToInfo(QueuedAction queued) =>
        new(
            queued.ActionId,
            queued.Label,
            queued.Phase,
            queued.ActorId,
            queued.TargetId,
            queued.Description,
            queued.Parameters);

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
        IReadOnlyDictionary<string, string> Parameters);

    private sealed record AutonomousQueuedAction(
        string ActionId,
        Guid ActorId,
        Guid TargetId);
}
