namespace Dynastia.Contracts;

public sealed class GameActionContext
{
    public GameActionContext(
        IGameState gameState,
        IPerson actor,
        IPerson target,
        IGameEventBus eventBus,
        IGameRandom random,
        IReadOnlyDictionary<string, string>? parameters = null,
        ActionExecutionContext? execution = null)
    {
        GameState = gameState;
        Actor = actor;
        Target = target;
        EventBus = eventBus;
        Random = random;
        Parameters = parameters ?? new Dictionary<string, string>();
        Execution = execution ?? ActionExecutionContext.Player;
    }

    public IGameState GameState { get; }
    public IPerson Actor { get; }
    public IPerson Target { get; }
    public IGameEventBus EventBus { get; }
    public IGameRandom Random { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }
    public ActionExecutionContext Execution { get; }

    public ActionExecutionOrigin Origin => Execution.Origin;
    public Guid? ActorHouseholdId => Execution.ActorHouseholdId;
    public YearPhase? ExecutionPhase => Execution.Phase;

    public bool ActorHasControl =>
        Actor.Tags.Has("control.playable")
        || Origin is ActionExecutionOrigin.Autonomous
            or ActionExecutionOrigin.System;
}
