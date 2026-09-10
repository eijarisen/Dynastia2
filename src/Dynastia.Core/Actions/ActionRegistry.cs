using Dynastia.Contracts;

namespace Dynastia.Core.Actions;

public sealed class ActionRegistry : IActionRegistry
{
    private readonly Dictionary<string, GameActionDefinition> _actions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<QueuedAction> _queued = [];

    private readonly IGameState _gameState;
    private readonly IGameEventBus _eventBus;
    private readonly IGameRandom _random;

    public ActionRegistry(
        IGameState gameState,
        IGameEventBus eventBus,
        IGameRandom random)
    {
        _gameState = gameState;
        _eventBus = eventBus;
        _random = random;
    }

    public void Register(GameActionDefinition action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!_actions.TryAdd(action.Id, action))
        {
            throw new InvalidOperationException(
                $"An action with ID '{action.Id}' is already registered.");
        }
    }

    public IReadOnlyList<GameActionDefinition> GetAvailableActions(
        IPerson actor,
        IPerson target)
    {
        if (_queued.Any(x => x.ActorId == actor.Id))
            return [];

        var context = CreateContext(actor, target);

        return _actions.Values
            .Where(action => action.IsAvailable(context))
            .OrderBy(action => action.Label)
            .ToList();
    }

    public GameActionResult Execute(
        string actionId,
        IPerson actor,
        IPerson target)
    {
        if (!_actions.TryGetValue(actionId, out var action))
        {
            return new GameActionResult(
                false,
                $"Unknown action '{actionId}'.");
        }

        var context = CreateContext(actor, target);

        if (!action.IsAvailable(context))
        {
            return new GameActionResult(
                false,
                "This action is no longer available.");
        }

        if (action.Mode == ActionExecutionMode.Queued)
        {
            if (_queued.Any(x => x.ActorId == actor.Id))
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

        return action.Execute(context);
    }

    public IReadOnlyList<QueuedActionInfo> GetQueuedActions(
        IPerson actor)
    {
        return _queued
            .Where(x => x.ActorId == actor.Id)
            .Select(x =>
            {
                var label =
                    _actions.TryGetValue(x.ActionId, out var action)
                        ? action.Label
                        : x.ActionId;

                return new QueuedActionInfo(
                    x.ActionId,
                    label,
                    x.Phase,
                    x.ActorId,
                    x.TargetId);
            })
            .ToList();
    }

    public void CancelQueuedActions(
        IPerson actor)
    {
        _queued.RemoveAll(
            x => x.ActorId == actor.Id);
    }

    public void ExecuteQueued(
        YearPhase phase)
    {
        var pending =
            _queued
                .Where(x => x.Phase == phase)
                .ToList();

        _queued.RemoveAll(
            x => x.Phase == phase);

        foreach (var queued in pending)
        {
            if (!_actions.TryGetValue(
                queued.ActionId,
                out var action))
            {
                continue;
            }

            var actor =
                _gameState.People.FirstOrDefault(
                    x => x.Id == queued.ActorId);

            var target =
                _gameState.People.FirstOrDefault(
                    x => x.Id == queued.TargetId);

            if (actor is null || target is null)
                continue;

            var context =
                CreateContext(actor, target);

            if (!action.IsAvailable(context))
                continue;

            action.Execute(context);
        }
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
