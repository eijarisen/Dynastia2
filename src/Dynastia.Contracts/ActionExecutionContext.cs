namespace Dynastia.Contracts;

public sealed record ActionExecutionContext(
    ActionExecutionOrigin Origin,
    Guid? ActorHouseholdId = null,
    YearPhase? Phase = null)
{
    public static ActionExecutionContext Player { get; } =
        new(ActionExecutionOrigin.Player);

    public static ActionExecutionContext Autonomous(
        Guid? actorHouseholdId = null,
        YearPhase? phase = null) =>
        new(
            ActionExecutionOrigin.Autonomous,
            actorHouseholdId,
            phase);

    public static ActionExecutionContext System(
        Guid? actorHouseholdId = null,
        YearPhase? phase = null) =>
        new(
            ActionExecutionOrigin.System,
            actorHouseholdId,
            phase);
}
