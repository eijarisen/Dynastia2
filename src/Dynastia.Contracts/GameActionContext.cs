namespace Dynastia.Contracts;

public sealed class GameActionContext
{
    public GameActionContext(
        IGameState gameState,
        IPerson actor,
        IPerson target,
        IGameEventBus eventBus,
        IGameRandom random,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        GameState = gameState;
        Actor = actor;
        Target = target;
        EventBus = eventBus;
        Random = random;
        Parameters = parameters ?? new Dictionary<string, string>();
    }

    public IGameState GameState { get; }
    public IPerson Actor { get; }
    public IPerson Target { get; }
    public IGameEventBus EventBus { get; }
    public IGameRandom Random { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }
}
