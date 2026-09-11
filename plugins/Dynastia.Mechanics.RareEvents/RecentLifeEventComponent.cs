namespace Dynastia.Mechanics.RareEvents;

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
