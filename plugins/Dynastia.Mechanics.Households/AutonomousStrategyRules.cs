namespace Dynastia.Mechanics.Households;

public static class AutonomousStrategyRules
{
    public const string PolicyVersion = "AUT-SURVIVAL-20260925";
    public const double CloseActionTolerance = 0.15;
    public const double MinimumUsefulRequestWillingness = 0.25;
    public const int ContinuityBuffer = 2;
    public const int DeliberateExpansionSoftStop = 2;
    public const int OverdueAfterMissedSafeOpportunities = 2;
    public const int BlockedReservationReleaseAssessments = 3;
    public const decimal ForecastExpenseStressMultiplier = 1.10m;
    public const decimal ForecastIncomeStressMultiplier = 0.75m;
    public const decimal ForecastReserveFraction = 0.25m;

    public static AutonomousFinancialState GetFinancialState(
        decimal wealth,
        decimal projectedIncome,
        decimal expectedExpenses)
    {
        var viableIncome = projectedIncome > 0;
        var annualGap = projectedIncome - expectedExpenses;
        var cushion = Math.Max(1000m, expectedExpenses);

        if (wealth < 0
            || (!viableIncome && wealth < cushion)
            || (annualGap < 0 && wealth <= 0))
        {
            return AutonomousFinancialState.Critical;
        }

        if (wealth <= 0
            || wealth < cushion * 0.5m
            || annualGap < 0)
        {
            return AutonomousFinancialState.Poor;
        }

        if (annualGap > 0
            && wealth >= cushion * 2m)
        {
            return AutonomousFinancialState.Secure;
        }

        return AutonomousFinancialState.Stable;
    }

    public static bool HasSustainableExpansionBudget(
        decimal wealth,
        decimal projectedIncome,
        decimal projectedExpensesWithChild)
    {
        if (projectedExpensesWithChild <= 0)
            return projectedIncome > 0 || wealth > 0;

        var stressedIncome = projectedIncome * ForecastIncomeStressMultiplier;
        var stressedExpenses = projectedExpensesWithChild * ForecastExpenseStressMultiplier;
        var twoYearDeficit = Math.Max(0m, stressedExpenses - stressedIncome) * 2m;
        var reserve = projectedExpensesWithChild * ForecastReserveFraction;
        return wealth >= twoYearDeficit + reserve;
    }

    public static bool CanActivelyTryForChild(
        int existingChildren,
        AutonomousFinancialState financialState,
        bool hasMaterialUnmetDependentNeed,
        bool strained,
        bool overcrowded,
        int dependentChildren,
        int effectiveCapacity,
        bool hasReproductivePath,
        bool hasSustainableBudget)
    {
        if (!hasReproductivePath
            || existingChildren >= DeliberateExpansionSoftStop
            || financialState is AutonomousFinancialState.Critical
                or AutonomousFinancialState.Poor
            || hasMaterialUnmetDependentNeed
            || strained
            || overcrowded
            || !hasSustainableBudget)
        {
            return false;
        }

        return dependentChildren + 1 <= effectiveCapacity;
    }

    public static bool CanSearchForReproductiveFemale(
        int seekerAge,
        bool goodMorals,
        bool evilMorals,
        bool homosexual)
    {
        if (homosexual)
            return false;

        if (evilMorals)
            return true;

        var maximumGap = goodMorals ? 10 : 20;
        var youngestAllowed = Math.Max(18, seekerAge - maximumGap);
        return youngestAllowed <= 45;
    }

    public static bool IsCloseEnoughToCompete(
        double score,
        double bestScore)
    {
        if (bestScore <= 0)
            return false;

        return score >= bestScore * (1.0 - CloseActionTolerance);
    }
}
