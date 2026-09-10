namespace Dynastia.Contracts;

public sealed class GameActionContext
{
    public GameActionContext(
        IGameState gameState,
        IPerson actor,
        IPerson target,
        IGameEventBus eventBus,
        IGameRandom random)
    {
        GameState = gameState;
        Actor = actor;
        Target = target;
        EventBus = eventBus;
        Random = random;
    }

    public IGameState GameState { get; }
    public IPerson Actor { get; }
    public IPerson Target { get; }
    public IGameEventBus EventBus { get; }
    public IGameRandom Random { get; }
}
