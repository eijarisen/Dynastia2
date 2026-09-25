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
    public const int MaleLineContinuity = 700;
    public const int BloodlineContinuity = 650;
    public const int FamilyContinuity = BloodlineContinuity;
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
    // Descendants include adults living elsewhere and descendants of deceased
    // children. Household membership and direct child counts are insufficient
    // to determine whether the dynasty can continue.
    public IReadOnlyList<IPerson> LivingMaleLineDescendants { get; init; } = [];
    public IReadOnlyList<IPerson> LivingBloodlineDescendants { get; init; } = [];
    public int ViableMaleLineDescendantCount { get; init; }
    public int ViableBloodlineDescendantCount { get; init; }
    public bool HasSecuredMaleLine { get; init; }
    public bool HasSecuredBloodline { get; init; }
    public bool NeedsMaleLineContinuity { get; init; }
    public bool NeedsBloodlineContinuity { get; init; }
    public bool NeedsFamilyContinuity => NeedsMaleLineContinuity || NeedsBloodlineContinuity;
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
}
