using Dynastia.Contracts;

namespace Dynastia.Core.Actions;

public sealed class ActionRegistry : IActionRegistry
{
    private readonly Dictionary<string, GameActionDefinition> _actions =
        new(StringComparer.OrdinalIgnoreCase);

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

        if (action.Mode != ActionExecutionMode.Immediate)
        {
            return new GameActionResult(
                false,
                "Queued actions are not implemented yet.");
        }

        return action.Execute(context);
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
}
