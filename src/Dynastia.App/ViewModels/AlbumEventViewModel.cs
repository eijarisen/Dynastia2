using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class AlbumEventViewModel
{
    public AlbumEventViewModel(
        GameEvent gameEvent,
        long scoreDelta = 0)
    {
        Event = gameEvent;
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
                EventEmojiMap.GetEmoji(
                    Event.Type);

            return string.IsNullOrWhiteSpace(
                emoji)
                    ? text
                    : $"{emoji} {text}";
        }
    }
}
