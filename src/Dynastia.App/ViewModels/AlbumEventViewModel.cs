using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class AlbumEventViewModel
{
    private readonly IEventPresentationRegistry _eventPresentation;

    public AlbumEventViewModel(
        GameEvent gameEvent,
        IEventPresentationRegistry eventPresentation,
        long scoreDelta = 0)
    {
        Event = gameEvent;
        _eventPresentation = eventPresentation;
        ScoreDelta = scoreDelta;
    }

    public GameEvent Event { get; }

    public long ScoreDelta { get; }
    public bool HasScoreDelta => ScoreDelta != 0;
    public string ScoreDeltaText => ScoreDelta > 0 ? $"+{ScoreDelta:N0}" : ScoreDelta.ToString("N0");

    public string Text
    {
        get
        {
            var text =
                Event.Data.TryGetValue(
                    "text",
                    out var value)
                    ? value
                    : Event.Type;

            var emoji =
                _eventPresentation.Resolve(
                    Event.Type).Emoji;

            return string.IsNullOrWhiteSpace(emoji)
                ? text
                : $"{emoji} {text}";
        }
    }
}
