namespace Dynastia.Contracts;

public sealed record QueuedActionInfo(
    string ActionId,
    string Label,
    YearPhase Phase,
    Guid ActorId,
    Guid TargetId,
    string? Description = null);
