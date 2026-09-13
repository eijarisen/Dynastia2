using Dynastia.Contracts;

namespace Dynastia.Core.Actions;

public sealed class QueuedActionYearSystem : IYearSystem
{
    private readonly IActionRegistry _actions;

    public QueuedActionYearSystem(
        IActionRegistry actions,
        YearPhase phase,
        string id)
    {
        _actions = actions;
        Phase = phase;
        Id = id;
    }

    public string Id { get; }

    public YearPhase Phase { get; }

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        _actions.ExecuteQueued(Phase);
    }
}
