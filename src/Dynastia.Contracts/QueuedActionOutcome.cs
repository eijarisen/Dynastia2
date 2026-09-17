namespace Dynastia.Contracts;

public enum QueuedActionResultCategory
{
    ExecutedSuccessfully = 0,
    ExecutedWithFailure = 1,
    Invalidated = 2,
    ActorMissing = 3,
    TargetMissing = 4,
    ActionMissing = 5,
    ActorBlocked = 6
}

public sealed record QueuedActionOutcome(
    string ActionId,
    Guid ActorId,
    Guid TargetId,
    YearPhase Phase,
    QueuedActionResultCategory Category,
    string ReasonCode,
    string Label,
    string? Message = null,
    IReadOnlyDictionary<string, string>? Details = null);
