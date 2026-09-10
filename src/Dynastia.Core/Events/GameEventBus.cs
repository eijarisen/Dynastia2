using Dynastia.Contracts;

namespace Dynastia.Core.Events;

public sealed class GameEventBus :
    IGameEventBus
{
    private readonly List<GameEvent>
        _events = [];

    public event EventHandler<GameEvent>?
        EventPublished;

    public IReadOnlyList<GameEvent> AllEvents =>
        _events;

    public void Publish(
        GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(
            gameEvent);

        _events.Add(
            gameEvent);

        EventPublished?.Invoke(
            this,
            gameEvent);
    }

    public IReadOnlyList<GameEvent> GetEventsForYear(
        int year)
    {
        return _events
            .Where(
                gameEvent =>
                    gameEvent.Year == year)
            .ToList();
    }

    public void RestoreEvents(
        IReadOnlyList<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(
            events);

        _events.Clear();
        _events.AddRange(
            events);
    }
}
