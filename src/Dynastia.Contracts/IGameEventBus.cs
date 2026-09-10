namespace Dynastia.Contracts;

public interface IGameEventBus
{
    event EventHandler<GameEvent>? EventPublished;

    void Publish(GameEvent gameEvent);

    IReadOnlyList<GameEvent> GetEventsForYear(int year);

    IReadOnlyList<GameEvent> AllEvents { get; }
}
