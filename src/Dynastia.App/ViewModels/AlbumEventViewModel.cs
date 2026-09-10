using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class AlbumEventViewModel
{
    public AlbumEventViewModel(
        GameEvent gameEvent)
    {
        Event = gameEvent;
    }

    public GameEvent Event { get; }

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
