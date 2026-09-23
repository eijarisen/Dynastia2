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

    public decimal? DisplayCost { get; init; }

    public ActionPresentationMetadata Presentation { get; init; } =
        ActionPresentationMetadata.Empty;

    // Used only by universal fallback actions such as Pass.
    // Normal gameplay actions remain subject to imprisonment/other guards.
    public bool BypassGuards { get; init; }

    // Legacy boolean availability remains the compatibility fallback.
    // New or migrated mechanics can provide EvaluateAvailability to return
    // stable reason IDs and resource/presentation metadata from the same rule
    // path used by UI, queueing and execution-time revalidation.
    public Func<GameActionContext, bool>? IsAvailable { get; init; }

    public Func<GameActionContext, ActionEvaluationResult>? EvaluateAvailability
    {
        get;
        init;
    }

    public required Func<GameActionContext, GameActionResult> Execute { get; init; }
}
