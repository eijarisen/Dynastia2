namespace Dynastia.Contracts;

public sealed record QueuedActionInfo(
    string ActionId,
    string Label,
    YearPhase Phase,
    Guid ActorId,
    Guid TargetId,
    string? Description = null,
    IReadOnlyDictionary<string, string>? Parameters = null,
    ActionExecutionOrigin Origin = ActionExecutionOrigin.Player,
    Guid? ActorHouseholdId = null);
