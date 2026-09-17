namespace Dynastia.Contracts;

public sealed record ActionEvaluationResult(
    bool Available,
    string ReasonCode,
    string? Reason = null)
{
    public static ActionEvaluationResult Allowed() =>
        new(true, ActionReasonCodes.Available);

    public static ActionEvaluationResult Denied(
        string reasonCode,
        string? reason = null) =>
        new(false, reasonCode, reason);
}
