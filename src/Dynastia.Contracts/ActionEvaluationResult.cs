namespace Dynastia.Contracts;

public sealed record ActionEvaluationResult
{
    public ActionEvaluationResult(
        bool available,
        string reasonCode,
        string? reason = null)
    {
        Available = available;
        ReasonCode = reasonCode;
        Reason = reason;
    }

    public bool Available { get; init; }

    public string ReasonCode { get; init; }

    public string? Reason { get; init; }

    public string? ActionId { get; init; }

    public Guid? ActorId { get; init; }

    public Guid? TargetId { get; init; }

    public Guid? HouseholdId { get; init; }

    public ActionExecutionMode? ExecutionMode { get; init; }

    public YearPhase? QueuePhase { get; init; }

    public IReadOnlyList<ActionResourceRequirement> ResourceRequirements { get; init; } =
        Array.Empty<ActionResourceRequirement>();

    public IReadOnlyDictionary<string, string> PresentationMetadata { get; init; } =
        new Dictionary<string, string>();

    public static ActionEvaluationResult Allowed(
        Guid? householdId = null,
        IReadOnlyList<ActionResourceRequirement>? resourceRequirements = null,
        IReadOnlyDictionary<string, string>? presentationMetadata = null) =>
        new(true, ActionReasonCodes.Available)
        {
            HouseholdId = householdId,
            ResourceRequirements =
                resourceRequirements
                ?? Array.Empty<ActionResourceRequirement>(),
            PresentationMetadata =
                presentationMetadata
                ?? new Dictionary<string, string>()
        };

    public static ActionEvaluationResult Denied(
        string reasonCode,
        string? reason = null,
        Guid? householdId = null,
        IReadOnlyList<ActionResourceRequirement>? resourceRequirements = null,
        IReadOnlyDictionary<string, string>? presentationMetadata = null) =>
        new(false, reasonCode, reason)
        {
            HouseholdId = householdId,
            ResourceRequirements =
                resourceRequirements
                ?? Array.Empty<ActionResourceRequirement>(),
            PresentationMetadata =
                presentationMetadata
                ?? new Dictionary<string, string>()
        };
}
