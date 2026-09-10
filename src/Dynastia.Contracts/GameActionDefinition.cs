namespace Dynastia.Contracts;

public sealed class GameActionDefinition
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }

    public ActionExecutionMode Mode { get; init; } =
        ActionExecutionMode.Immediate;

    public YearPhase QueuePhase { get; init; } =
        YearPhase.LifeEvents;

    public required Func<GameActionContext, bool> IsAvailable { get; init; }

    public required Func<GameActionContext, GameActionResult> Execute { get; init; }
}
