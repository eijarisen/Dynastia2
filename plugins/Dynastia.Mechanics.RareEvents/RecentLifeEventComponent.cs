using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

[PersistedComponentId("rare_events.recent_life_event")]
public sealed class RecentLifeEventComponent
{
    public List<RecentLifeEventFlagState> Flags { get; set; } = [];
}

public sealed class RecentLifeEventFlagState
{
    public string Tag { get; set; } =
        string.Empty;

    // The flag remains active through this game year.
    public int ExpiresAfterYear { get; set; }
}
