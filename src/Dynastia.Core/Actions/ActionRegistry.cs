using Dynastia.Contracts;

namespace Dynastia.Core.Actions;

public sealed class ActionRegistry : IActionRegistry
{
    private readonly Dictionary<
        string,
        GameActionDefinition>
        _actions =
            new(
                StringComparer.OrdinalIgnoreCase);

    private readonly List<QueuedAction>
        _queued = [];

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

    public void Register(
        GameActionDefinition action)
    {
        ArgumentNullException.ThrowIfNull(
            action);

        if (!_actions.TryAdd(
            action.Id,
            action))
        {
            throw new InvalidOperationException(
                $"An action with ID " +
                $"'{action.Id}' is already registered.");
        }
    }

    public IReadOnlyList<GameActionDefinition>
        GetAvailableActions(
            IPerson actor,
            IPerson target)
    {
        var guardResult =
            _guards.Evaluate(
                actor);

        if (_queued.Any(
            queued =>
                queued.ActorId
                == actor.Id))
        {
            return [];
        }

        var context =
            CreateContext(
                actor,
                target);

        return _actions.Values
            .Where(
                action =>
                    (
                        guardResult.Allowed
                        || action.BypassGuards
                    )
                    && action.IsAvailable(
                        context))
            .OrderBy(
                action =>
                    action.Label)
            .ToList();
    }

    public GameActionResult Execute(
        string actionId,
        IPerson actor,
        IPerson target)
    {
        if (!_actions.TryGetValue(
            actionId,
            out var action))
        {
            return new GameActionResult(
                false,
                $"Unknown action '{actionId}'.");
        }

        var guardResult =
            _guards.Evaluate(
                actor);

        if (!guardResult.Allowed
            && !action.BypassGuards)
        {
            return new GameActionResult(
                false,
                guardResult.Reason
                    ?? "This character cannot perform actions.");
        }

        var context =
            CreateContext(
                actor,
                target);

        if (!action.IsAvailable(
            context))
        {
            return new GameActionResult(
                false,
                "This action is no longer available.");
        }

        if (action.Mode
            == ActionExecutionMode.Queued)
        {
            if (_queued.Any(
                queued =>
                    queued.ActorId
                    == actor.Id))
            {
                return new GameActionResult(
                    false,
                    "This character already has a queued action.");
            }

            _queued.Add(
                new QueuedAction(
                    action.Id,
                    actor.Id,
                    target.Id,
                    action.QueuePhase));

            return new GameActionResult(
                true,
                $"{action.Label} queued.");
        }

        return action.Execute(
            context);
    }

    public IReadOnlyList<QueuedActionInfo>
        GetQueuedActions(
            IPerson actor)
    {
        return _queued
            .Where(
                queued =>
                    queued.ActorId
                    == actor.Id)
            .Select(
                queued =>
                {
                    var label =
                        _actions.TryGetValue(
                            queued.ActionId,
                            out var action)
                            ? action.Label
                            : queued.ActionId;

                    var description =
                        _actions.TryGetValue(
                            queued.ActionId,
                            out var definition)
                            ? definition.Description
                            : null;

                    return new QueuedActionInfo(
                        queued.ActionId,
                        label,
                        queued.Phase,
                        queued.ActorId,
                        queued.TargetId,
                        description);
                })
            .ToList();
    }

    public IReadOnlyList<QueuedActionInfo>
        GetAllQueuedActions()
    {
        return _queued
            .Select(
                queued =>
                {
                    var label =
                        _actions.TryGetValue(
                            queued.ActionId,
                            out var action)
                            ? action.Label
                            : queued.ActionId;

                    var description =
                        _actions.TryGetValue(
                            queued.ActionId,
                            out var definition)
                            ? definition.Description
                            : null;

                    return new QueuedActionInfo(
                        queued.ActionId,
                        label,
                        queued.Phase,
                        queued.ActorId,
                        queued.TargetId,
                        description);
                })
            .ToList();
    }


    public void CancelQueuedActions(
        IPerson actor)
    {
        _queued.RemoveAll(
            queued =>
                queued.ActorId
                == actor.Id);
    }

    public void RestoreQueuedActions(
        IReadOnlyList<QueuedActionInfo> queuedActions)
    {
        ArgumentNullException.ThrowIfNull(
            queuedActions);

        var restored =
            new List<QueuedAction>();

        var actors =
            new HashSet<Guid>();

        foreach (var saved in
            queuedActions)
        {
            if (!_actions.TryGetValue(
                saved.ActionId,
                out var definition))
            {
                throw new InvalidDataException(
                    $"Save file references unknown action " +
                    $"'{saved.ActionId}'.");
            }

            if (!actors.Add(
                saved.ActorId))
            {
                throw new InvalidDataException(
                    "Save file contains more than one queued " +
                    $"action for actor {saved.ActorId}.");
            }

            if (!_gameState.People.Any(
                    person =>
                        person.Id
                        == saved.ActorId)
                || !_gameState.People.Any(
                    person =>
                        person.Id
                        == saved.TargetId))
            {
                throw new InvalidDataException(
                    $"Queued action '{saved.ActionId}' " +
                    "references a missing person.");
            }

            // Queue phase is owned by the current action definition.
            // This keeps old save files compatible if only display
            // metadata changed, while rejecting removed action IDs.
            restored.Add(
                new QueuedAction(
                    definition.Id,
                    saved.ActorId,
                    saved.TargetId,
                    definition.QueuePhase));
        }

        _queued.Clear();
        _queued.AddRange(
            restored);
    }

    public void ExecuteQueued(
        YearPhase phase)
    {
        var pending =
            _queued
                .Where(
                    queued =>
                        queued.Phase
                        == phase)
                .ToList();

        _queued.RemoveAll(
            queued =>
                queued.Phase
                == phase);

        foreach (var queued in
            pending)
        {
            if (!_actions.TryGetValue(
                queued.ActionId,
                out var action))
            {
                continue;
            }

            var actor =
                _gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id
                            == queued.ActorId);

            var target =
                _gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id
                            == queued.TargetId);

            if (actor is null
                || target is null)
            {
                continue;
            }

            var guardResult =
                _guards.Evaluate(
                    actor);

            if (!guardResult.Allowed
                && !action.BypassGuards)
            {
                continue;
            }

            var context =
                CreateContext(
                    actor,
                    target);

            if (!action.IsAvailable(
                context))
            {
                continue;
            }

            action.Execute(
                context);
        }
    }

    public string? GetBlockedReason(
        IPerson actor)
    {
        var result =
            _guards.Evaluate(
                actor);

        return result.Allowed
            ? null
            : result.Reason;
    }

    private GameActionContext CreateContext(
        IPerson actor,
        IPerson target)
    {
        return new GameActionContext(
            _gameState,
            actor,
            target,
            _eventBus,
            _random);
    }

    private sealed record QueuedAction(
        string ActionId,
        Guid ActorId,
        Guid TargetId,
        YearPhase Phase);
}
