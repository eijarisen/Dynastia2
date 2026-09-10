using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class AlbumEventViewModel
{
    public AlbumEventViewModel(GameEvent gameEvent)
    {
        Event = gameEvent;
    }

    public GameEvent Event { get; }

    public string Text =>
        Event.Data.TryGetValue("text", out var text)
            ? text
            : Event.Type;
}
