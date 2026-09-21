using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public static class MarriageBalanceRules
{
    public const double MiserableThreshold = 20;
    public const double UnhappyThreshold = 40;

    // Passive repair is intentionally weak. Reconciliation is the meaningful
    // active route back from a damaged marriage.
    public const double AnnualStability = 1;

    public const double UnemployedHusbandPenalty = 7;
    public const double UnemployedWorkingSpousePenalty = 3;
    public const double ImprisonmentPenalty = 10;
    public const double WorkHarderPenalty = 4;
    public const double CrimePenalty = 5;
    public const double UncaughtCrimePenalty = 2.5;
    public const double AffairPenalty = 25;
    public const double LowFertilityPenalty = 1.25;
    public const double LowIntellectPenalty = 0.625;

    public static double GetAnnualSatisfactionChange(
        double totalPenalty) =>
        AnnualStability - Math.Max(0, totalPenalty);

    public static double GetFinancialPressurePenalty(
        bool isBroke,
        bool hasWorkingSpouse)
    {
        if (!isBroke)
            return 0;

        return hasWorkingSpouse
            ? 2.5
            : 6.0;
    }

    public static double GetLowAttractionPenalty(
        int husbandAppeal,
        int wifeAppeal,
        bool wifeRetired = false)
    {
        if (wifeRetired)
            return 0;

        var husband = Math.Clamp(husbandAppeal, 1, 5);
        var wife = Math.Clamp(wifeAppeal, 1, 5);

        // Low appeal only creates recurring pressure when the husband is
        // meaningfully more appealing than his wife. Couples with similar
        // appeal do not penalize one another simply for both being unattractive.
        if (husband - wife < 2)
            return 0;

        return AppealPenalty(wife);
    }


    public static bool ShouldApplyLowFertilityPenalty(
        int wifeAge,
        int fertility) =>
        wifeAge <= 40
        && Math.Clamp(fertility, 0, 5) <= 2;

    public static bool ShouldApplyLowIntellectPenalty(
        int intellect) =>
        Math.Clamp(intellect, 1, 5) == 1;

    public static double GetPersonalityIncompatibilityPenalty(
        PersonalitySnapshot? first,
        PersonalitySnapshot? second)
    {
        if (first is null || second is null)
            return 0;

        var firstMorals = first.Morals.ToLowerInvariant();
        var secondMorals = second.Morals.ToLowerInvariant();

        return (firstMorals.Equals("good", StringComparison.OrdinalIgnoreCase)
                && secondMorals.Equals("evil", StringComparison.OrdinalIgnoreCase))
            || (firstMorals.Equals("evil", StringComparison.OrdinalIgnoreCase)
                && secondMorals.Equals("good", StringComparison.OrdinalIgnoreCase))
                ? 1.5
                : 0;
    }

    public static double GetSeriousIllnessPenalty(
        HealthSnapshot health)
    {
        ArgumentNullException.ThrowIfNull(health);

        var penalty = health.Percentage switch
        {
            < 35 => 4.0,
            < 60 => 2.5,
            _ => 0
        };

        foreach (var condition in health.Conditions)
        {
            var conditionPenalty =
                condition.Type.Equals("terminal", StringComparison.OrdinalIgnoreCase)
                    ? 4.5
                    : condition.HealthImpact <= -10
                        ? 3.5
                        : condition.HealthImpact <= -5
                            ? 2.0
                            : condition.Type.Equals("permanent", StringComparison.OrdinalIgnoreCase)
                                && condition.HealthImpact <= -3
                                ? 1.5
                                : 0;

            penalty = Math.Max(penalty, conditionPenalty);
        }

        return penalty;
    }

    public static double GetPoorFamilyRelationsPenalty(
        IEnumerable<double> sympathyScores)
    {
        ArgumentNullException.ThrowIfNull(sympathyScores);

        var penalty = 0.0;
        foreach (var sympathy in sympathyScores)
        {
            if (sympathy < 20)
                penalty += 1.0;
            else if (sympathy < 40)
                penalty += 0.5;
        }

        return Math.Min(3.0, penalty);
    }

    public static double GetAutomaticDivorceChance(
        double satisfaction)
    {
        return satisfaction switch
        {
            < 10 => 0.20,
            < MiserableThreshold => 0.10,
            < 30 => 0.015,
            < UnhappyThreshold => 0.005,
            _ => 0
        };
    }

    private static double AppealPenalty(int appeal) =>
        Math.Clamp(appeal, 1, 5) switch
        {
            1 => 1.50,
            2 => 0.75,
            _ => 0
        };
}
