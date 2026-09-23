using Dynastia.Contracts;

namespace Dynastia.Core.Events;

public sealed class GameEventBus :
    IGameEventBus
{
    private readonly List<GameEvent>
        _events = [];

    private readonly Dictionary<int, List<GameEvent>>
        _eventsByYear = [];

    private readonly Dictionary<int, IReadOnlyList<GameEvent>>
        _yearSnapshots = [];

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

        if (!_eventsByYear.TryGetValue(
                gameEvent.Year,
                out var yearEvents))
        {
            yearEvents = [];
            _eventsByYear.Add(
                gameEvent.Year,
                yearEvents);
        }

        yearEvents.Add(
            gameEvent);

        _yearSnapshots.Remove(
            gameEvent.Year);

        EventPublished?.Invoke(
            this,
            gameEvent);
    }

    public IReadOnlyList<GameEvent> GetEventsForYear(
        int year)
    {
        if (_yearSnapshots.TryGetValue(
                year,
                out var snapshot))
        {
            return snapshot;
        }

        snapshot = _eventsByYear.TryGetValue(
                year,
                out var yearEvents)
            ? Array.AsReadOnly(
                yearEvents.ToArray())
            : Array.Empty<GameEvent>();

        _yearSnapshots[year] =
            snapshot;

        return snapshot;
    }

    public void RestoreEvents(
        IReadOnlyList<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(
            events);

        _events.Clear();
        _eventsByYear.Clear();
        _yearSnapshots.Clear();

        foreach (var gameEvent in events)
        {
            _events.Add(
                gameEvent);

            if (!_eventsByYear.TryGetValue(
                    gameEvent.Year,
                    out var yearEvents))
            {
                yearEvents = [];
                _eventsByYear.Add(
                    gameEvent.Year,
                    yearEvents);
            }

            yearEvents.Add(
                gameEvent);
        }
    }
}
