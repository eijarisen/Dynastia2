using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public enum AutonomousFinancialState
{
    Critical = 0,
    Poor = 1,
    Stable = 2,
    Secure = 3
}

internal enum AutonomyCategory
{
    Survival,
    Solvency,
    Continuity,
    ChildProtection,
    RelationshipStability,
    CareerDevelopment,
    FamilyRelations,
    Property,
    PersonalDevelopment,
    Optional
}

internal static class AutonomousPriorityBands
{
    public const int EmergencySurvival = 1000;
    public const int HouseholdSolvency = 800;
    public const int SustainableFamilyContinuity = 650;
    // Compatibility aliases intentionally resolve to the same band. Ancestry
    // is a diagnostic/tie-break fact, never a separate autonomous urgency.
    public const int MaleLineContinuity = SustainableFamilyContinuity;
    public const int BloodlineContinuity = SustainableFamilyContinuity;
    public const int FamilyContinuity = SustainableFamilyContinuity;
    public const int FamilyStability = 500;
    public const int LongTermImprovement = 300;
    public const int OptionalDevelopment = 100;
}

internal sealed record AutonomousMemberSnapshot(
    IPerson Person,
    HealthSnapshot Health,
    CareerSnapshot? Career,
    IReadOnlyDictionary<string, int> Stats,
    bool IsChild,
    bool IsDependent,
    bool IsSeriousHealthRisk,
    bool IsImmediateHealthRisk)
{
    public bool IsMaleLineage { get; init; }
    public bool IsBloodline { get; init; }
}

internal sealed record AutonomousHouseholdSnapshot(
    HouseholdInfo Household,
    IPerson Head,
    HouseholdFinanceSnapshot? Finance,
    HouseholdStatusSnapshot? Status,
    IReadOnlyList<AutonomousMemberSnapshot> Members,
    IPerson? Spouse,
    IReadOnlyList<IPerson> LivingChildren,
    AutonomousFinancialState FinancialState,
    decimal ProjectedIncome,
    decimal ExpectedExpenses,
    decimal DebtPayments,
    bool HasImmediateMedicalDanger,
    bool HasSeriousMedicalDanger,
    bool HasInvestmentHouse,
    bool HasResidence,
    bool HasRealisticReproductivePath,
    bool CanActivelyTryForChild,
    int LivingChildCount,
    int DependentChildCount,
    double? MarriageSatisfaction,
    double ReproductiveUrgency,
    IReadOnlyList<RelatedFamilyHouseholdInfo> RelatedHouseholds)
{
    // Existing-child commitments are deliberately separate from biological
    // succession diagnostics. An ill, infertile, adult, adopted or departed
    // child remains a family commitment and never creates a replacement-birth
    // quota merely by becoming a weak succession carrier.
    public IReadOnlyList<IPerson> ExistingChildren { get; init; } = [];
    public int ExistingChildCount => ExistingChildren.Count;
    public bool HasMaterialUnmetDependentNeed { get; init; }
    public bool HasAdultFamilyFormationNeed { get; init; }
    public bool HasEstablishedDescendantFamily { get; init; }
    public bool NeedsFamilyExpansion { get; init; }

    // Descendants remain available for canonical succession diagnostics and a
    // final tie-break between otherwise equivalent family-formation plans.
    public IReadOnlyList<IPerson> LivingMaleLineDescendants { get; init; } = [];
    public IReadOnlyList<IPerson> LivingBloodlineDescendants { get; init; } = [];
    public int ViableMaleLineDescendantCount { get; init; }
    public int ViableBloodlineDescendantCount { get; init; }
    public bool HasSecuredMaleLine { get; init; }
    public bool HasSecuredBloodline { get; init; }
    public bool NeedsMaleLineContinuity { get; init; }
    public bool NeedsBloodlineContinuity { get; init; }

    public bool NeedsFamilyContinuity =>
        NeedsFamilyExpansion || HasAdultFamilyFormationNeed;

    public decimal ProtectedPlanReserve { get; init; }
    public bool HasProtectedFamilyPlan { get; init; }
}

internal sealed record AutonomousActionCandidate(
    GameActionDefinition Action,
    IPerson Target,
    IReadOnlyDictionary<string, string> Parameters,
    AutonomyCategory Category,
    int PriorityBand,
    double Score,
    double? RequestWillingness = null)
{
    public int LineagePriority { get; init; }
    public double ExpectedGameScore { get; init; }
    public string? PlanGoalId { get; init; }
    public Guid? PlanBeneficiaryId { get; init; }
    public int? PlanFirstReadyYear { get; init; }
    public int? PlanLastServedYear { get; init; }
    public int PlanMissedSafeOpportunities { get; init; }
    public bool PlanIsOverdue { get; init; }
    public int PlanDeadlineUrgency { get; init; }
}
