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
        int firstAppeal,
        int secondAppeal) =>
        AppealPenalty(firstAppeal)
        + AppealPenalty(secondAppeal);

    public static double GetPersonalityIncompatibilityPenalty(
        PersonalitySnapshot? first,
        PersonalitySnapshot? second)
    {
        if (first is null || second is null)
            return 0;

        var firstTemperament = first.Temperament.ToLowerInvariant();
        var secondTemperament = second.Temperament.ToLowerInvariant();
        var temperaments = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            firstTemperament,
            secondTemperament
        };

        var temperamentPenalty =
            firstTemperament.Equals(secondTemperament, StringComparison.OrdinalIgnoreCase)
                ? 0
                : temperaments.SetEquals(["choleric", "melancholic"])
                    ? 2.0
                    : temperaments.SetEquals(["choleric", "phlegmatic"])
                        ? 1.5
                        : temperaments.SetEquals(["melancholic", "sanguine"])
                            ? 1.25
                            : 0.75;

        var firstMorals = first.Morals.ToLowerInvariant();
        var secondMorals = second.Morals.ToLowerInvariant();
        var morals = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            firstMorals,
            secondMorals
        };

        var moralsPenalty =
            firstMorals.Equals(secondMorals, StringComparison.OrdinalIgnoreCase)
                ? 0
                : morals.SetEquals(["good", "evil"])
                    ? 2.0
                    : morals.Contains("evil")
                        ? 1.0
                        : 0.25;

        return temperamentPenalty + moralsPenalty;
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
            1 => 2.5,
            2 => 1.5,
            _ => 0
        };
}
