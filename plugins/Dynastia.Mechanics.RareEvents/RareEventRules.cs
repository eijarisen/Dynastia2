using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

public static class RareEventRules
{
    public const double MaximumSuicideChance = 0.0004;
    public const double BaseSuicideChance = 0.000005;

    public static double GetStatMultiplier(string direction, int value)
    {
        var stat = Math.Clamp(value, 1, 5);
        if (direction.Equals("High", StringComparison.OrdinalIgnoreCase))
            return 0.8 + (stat - 1) * 0.1;
        if (direction.Equals("Low", StringComparison.OrdinalIgnoreCase))
            return 1.2 - (stat - 1) * 0.1;
        return 1.0;
    }

    public static double GetTownPreferenceMultiplier(string preference, SettlementClass settlementClass)
    {
        if (preference.Equals("Urban", StringComparison.OrdinalIgnoreCase))
            return settlementClass switch
            {
                SettlementClass.SmallTown => 0.80,
                SettlementClass.Town => 0.95,
                SettlementClass.City => 1.15,
                SettlementClass.MajorCity => 1.30,
                _ => 1.0
            };
        if (preference.Equals("Rural", StringComparison.OrdinalIgnoreCase))
            return settlementClass switch
            {
                SettlementClass.SmallTown => 1.30,
                SettlementClass.Town => 1.10,
                SettlementClass.City => 0.90,
                SettlementClass.MajorCity => 0.75,
                _ => 1.0
            };
        return 1.0;
    }

    public static double GetPreferredOpportunityMultiplier(
        IReadOnlyCollection<string> preferredTags,
        IReadOnlySet<string> availableTags) =>
        preferredTags.Count == 0 ? 1.0 : preferredTags.Any(availableTags.Contains) ? 1.25 : 1.0;

    public static double GetPreferredCareerFamilyMultiplier(
        IReadOnlyCollection<string> preferredFamilies,
        string? careerFamily) =>
        string.IsNullOrWhiteSpace(careerFamily) || preferredFamilies.Count == 0
            ? 1.0
            : preferredFamilies.Contains(careerFamily, StringComparer.OrdinalIgnoreCase) ? 1.15 : 1.0;

    public static double GetSuicideChance(
        double stress,
        bool depression,
        bool anxiety,
        bool alcoholism,
        bool drugDependence,
        bool bereavement,
        bool divorce,
        bool jobLossAndPoverty,
        bool veryLowHealth,
        string? temperament)
    {
        if (stress < 5) return 0.0;

        var chance = BaseSuicideChance * Math.Clamp(1.0 + (stress - 5.0) * 0.15, 1.0, 2.5);
        if (depression) chance *= 20.0;
        if (bereavement) chance *= 3.0;
        if (alcoholism) chance *= 3.0;
        if (drugDependence) chance *= 3.0;
        if (anxiety) chance *= 2.0;
        if (divorce) chance *= 2.0;
        if (jobLossAndPoverty) chance *= 2.0;
        if (veryLowHealth) chance *= 2.0;

        chance *= temperament?.ToLowerInvariant() switch
        {
            "melancholic" => 1.35,
            "choleric" => 1.20,
            "sanguine" => 0.75,
            "phlegmatic" => 0.80,
            _ => 1.0
        };

        return Math.Min(chance, MaximumSuicideChance);
    }
}
