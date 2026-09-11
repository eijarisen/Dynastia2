using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed class RecentLifeEventCleanupYearSystem :
    IYearSystem
{
    private readonly RecentLifeEventTracker _tracker;

    public RecentLifeEventCleanupYearSystem(
        RecentLifeEventTracker tracker)
    {
        _tracker =
            tracker;
    }

    public string Id =>
        "rare_events.recent_flags_cleanup";

    public YearPhase Phase =>
        YearPhase.PreYear;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        _tracker.Cleanup(
            gameState.Year);
    }
}
