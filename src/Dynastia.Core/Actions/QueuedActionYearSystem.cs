using Dynastia.Contracts;

namespace Dynastia.Core.Actions;

public sealed class QueuedActionYearSystem : IYearSystem
{
    private readonly IActionRegistry _actions;
    private readonly IReadOnlyCollection<string> _before;
    private readonly IReadOnlyCollection<string> _after;

    public QueuedActionYearSystem(
        IActionRegistry actions,
        YearPhase phase,
        string id,
        IReadOnlyCollection<string>? before = null,
        IReadOnlyCollection<string>? after = null)
    {
        _actions = actions;
        Phase = phase;
        Id = id;
        _before = before ?? Array.Empty<string>();
        _after = after ?? Array.Empty<string>();
    }

    public string Id { get; }

    public YearPhase Phase { get; }

    public IReadOnlyCollection<string> Before => _before;

    public IReadOnlyCollection<string> After => _after;

    public void Execute(IGameState gameState)
    {
        _actions.ExecuteQueued(Phase);
    }
}
