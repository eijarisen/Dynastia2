using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

/// <summary>
/// Durable, minimal autonomy intent. It records fairness/reservation state only;
/// all legality, prices, forecasts and candidate objects are recomputed live.
/// </summary>
[PersistedComponentId("households.autonomy_family_plan")]
public sealed class AutonomousFamilyPlanComponent
{
    public string PolicyVersion { get; set; } = AutonomousStrategyRules.PolicyVersion;
    public string GoalId { get; set; } = string.Empty;
    public Guid SourceHouseholdId { get; set; }
    public Guid BeneficiaryId { get; set; }
    public int CreatedYear { get; set; }
    public int? FirstReadyYear { get; set; }
    public int? LastProgressYear { get; set; }
    public int? LastServedYear { get; set; }
    public int MissedSafeOpportunities { get; set; }
    public int ConsecutiveBlockedReassessments { get; set; }
    public decimal ReservedCash { get; set; }
    public List<Guid> ReservedAssetIds { get; set; } = [];
    public string? BlockerCode { get; set; }
    public int? RetryYear { get; set; }
    public string? PendingActionId { get; set; }
    public int? PendingActionYear { get; set; }
    public int PendingBaselineChildCount { get; set; }
    public Guid? PendingBaselineSpouseId { get; set; }
    public bool PendingBaselineEmployed { get; set; }
}
