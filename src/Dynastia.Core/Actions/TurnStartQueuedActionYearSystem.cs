using Dynastia.Contracts;

namespace Dynastia.Core.Actions;

public sealed class TurnStartQueuedActionYearSystem : IYearSystem
{
    private readonly IActionRegistry _actions;

    public TurnStartQueuedActionYearSystem(IActionRegistry actions)
    {
        _actions = actions;
    }

    public string Id => "actions.queued.turn_start";
    public YearPhase Phase => YearPhase.TurnStartActions;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState) =>
        _actions.ExecuteTurnStartQueuedActions();
}
