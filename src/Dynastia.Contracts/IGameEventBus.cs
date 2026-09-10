namespace Dynastia.Contracts;

public interface IGameEventBus
{
    event EventHandler<GameEvent>? EventPublished;

    void Publish(
        GameEvent gameEvent);

    IReadOnlyList<GameEvent> GetEventsForYear(
        int year);

    IReadOnlyList<GameEvent> AllEvents { get; }

    /// <summary>
    /// Replaces persisted event history without publishing the events again.
    /// This is used by Save/Load so gameplay subscribers are not re-triggered.
    /// </summary>
    void RestoreEvents(
        IReadOnlyList<GameEvent> events);
}
