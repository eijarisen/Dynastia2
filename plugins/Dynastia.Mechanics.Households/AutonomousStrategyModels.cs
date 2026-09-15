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
    public const int FamilyContinuity = 650;
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
    bool IsImmediateHealthRisk);

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
    IReadOnlyList<RelatedFamilyHouseholdInfo> RelatedHouseholds);

internal sealed record AutonomousActionCandidate(
    GameActionDefinition Action,
    IPerson Target,
    IReadOnlyDictionary<string, string> Parameters,
    AutonomyCategory Category,
    int PriorityBand,
    double Score,
    double? RequestWillingness = null);
