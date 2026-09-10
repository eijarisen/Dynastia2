using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class AlbumEventViewModel
{
    public AlbumEventViewModel(GameEvent gameEvent)
    {
        Event = gameEvent;
    }

    public GameEvent Event { get; }

    // Temporary fallback display.
    // Later a text/localization service will format event types + data.
    public string Text => Event.Type;
}
