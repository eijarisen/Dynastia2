using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

public static class CraftRules
{
    public const int MaximumCrafts = 2;
    public const double PassiveLearningChance = 0.05;
    public const decimal EducationCost = 3000m;
    public const decimal MonthlyIncomeBase = 40m;

    private static readonly CraftMasteryRule[] MasteryRules =
    [
        new(1, "Novice", 1, 0.0, 0, 1609.61m),
        new(2, "Apprentice", 2, 3.0, 0, 1742.49m),
        new(3, "Adept", 3, 8.0, 1, 1924.77m),
        new(4, "Expert", 4, 15.0, 3, 2208.61m),
        new(5, "Master", 5, 25.0, 8, 2806.17m)
    ];

    public static IReadOnlyList<CraftMasteryRule> MasteryLevels => MasteryRules;

    public static CraftMasteryRule GetMasteryRule(int level) =>
        MasteryRules[Math.Clamp(level, 1, 5) - 1];

    public static int GetMasteryLevel(double masteryProgress, int relevantExperienceYears)
    {
        var progress = Math.Max(0.0, masteryProgress);
        var years = Math.Max(0, relevantExperienceYears);

        for (var index = MasteryRules.Length - 1; index >= 0; index--)
        {
            var rule = MasteryRules[index];
            if (progress + 0.0000001 >= rule.RequiredMasteryProgress
                && years >= rule.MinimumRelevantExperienceYears)
            {
                return rule.Level;
            }
        }

        return 1;
    }

    public static double GetExperienceProgressGain(int primaryStat) =>
        0.50 + 0.15 * Math.Clamp(primaryStat, 1, 5);

    public static double GetNewCraftStudyChance(int primaryStat) =>
        Math.Clamp(0.35 + 0.10 * Math.Clamp(primaryStat, 1, 5), 0.10, 0.95);

    public static double GetCraftImprovementChance(int primaryStat, int currentMasteryLevel) =>
        Math.Clamp(
            0.55
            + 0.08 * Math.Clamp(primaryStat, 1, 5)
            - 0.10 * Math.Clamp(currentMasteryLevel, 1, 5),
            0.10,
            0.85);

    public static double GetTeachingSuccessChance(
        CraftInfo craft,
        IReadOnlyDictionary<string, int> stats) =>
        Math.Clamp(0.40 + GetAptitudeScore(craft, stats) * 0.05, 0, 1);

    // Retained for compatibility with older unit tests/callers. New learning uses
    // the Craft-defined aptitude stats through the overload above.
    public static double GetTeachingSuccessChance(int intellect) =>
        Math.Clamp(0.40 + Math.Clamp(intellect, 1, 5) * 0.05, 0, 1);

    public static double GetApplicationBonus(
        IEnumerable<CraftInfo> knownCrafts,
        string careerId)
    {
        ArgumentNullException.ThrowIfNull(knownCrafts);
        ArgumentException.ThrowIfNullOrWhiteSpace(careerId);

        var best = 0.0;
        foreach (var craft in knownCrafts)
        {
            if (craft.PrimaryCareerIds.Contains(careerId, StringComparer.OrdinalIgnoreCase))
                best = Math.Max(best, 0.15);
            else if (craft.SecondaryCareerIds.Contains(careerId, StringComparer.OrdinalIgnoreCase))
                best = Math.Max(best, 0.08);
        }

        return best;
    }

    public static double GetGeneratedAdultBaseChance(int year)
    {
        if (year <= 1700)
            return 0.35;
        if (year <= 1850)
            return Interpolate(year, 1700, 1850, 0.35, 0.25);
        if (year <= 1950)
            return Interpolate(year, 1850, 1950, 0.25, 0.15);
        if (year <= 2000)
            return Interpolate(year, 1950, 2000, 0.15, 0.10);
        return 0.10;
    }

    public static double GetAptitudeScore(
        CraftInfo craft,
        IReadOnlyDictionary<string, int> stats)
    {
        ArgumentNullException.ThrowIfNull(craft);
        ArgumentNullException.ThrowIfNull(stats);

        var primary = GetStat(stats, craft.PrimaryStat);
        if (string.IsNullOrWhiteSpace(craft.SecondaryStat))
            return primary;

        var secondary = GetStat(stats, craft.SecondaryStat!);
        return primary * 0.75 + secondary * 0.25;
    }

    public static double GetStatSelectionMultiplier(
        CraftInfo craft,
        IReadOnlyDictionary<string, int> stats)
    {
        var primary = StatMultiplier(GetStat(stats, craft.PrimaryStat));
        if (string.IsNullOrWhiteSpace(craft.SecondaryStat))
            return primary;

        var secondaryBase = StatMultiplier(GetStat(stats, craft.SecondaryStat!));
        var secondary = 1.0 + (secondaryBase - 1.0) * 0.5;
        return primary * secondary;
    }

    public static double TownMultiplier(
        string preference,
        SettlementClass settlementClass) =>
        preference.ToLowerInvariant() switch
        {
            "rural" => settlementClass switch
            {
                SettlementClass.SmallTown => 1.50,
                SettlementClass.Town => 1.20,
                SettlementClass.City => 0.85,
                SettlementClass.MajorCity => 0.70,
                _ => 1.0
            },
            "urban" => settlementClass switch
            {
                SettlementClass.SmallTown => 0.70,
                SettlementClass.Town => 0.95,
                SettlementClass.City => 1.30,
                SettlementClass.MajorCity => 1.50,
                _ => 1.0
            },
            _ => 1.0
        };

    public static double PreferredOpportunityMultiplier(
        CraftInfo craft,
        IReadOnlySet<string> opportunityTags)
    {
        var matches = craft.PreferredOpportunityTags.Count(opportunityTags.Contains);
        return 1.0 + Math.Min(matches, 3) * 0.25;
    }

    public static decimal GetExpectedAnnualIncome(int masteryLevel) =>
        GetMasteryRule(masteryLevel).ExpectedAnnualIncome;

    public static decimal CalculateMonthlyIncome(double rawRoll, int masteryLevel)
    {
        var effectiveRoll = Math.Min(
            99.0,
            Math.Clamp(rawRoll, 0.0, 94.999999999) + GetMasteryRule(masteryLevel).MasteryBonus);

        return MonthlyIncomeBase
            * (decimal)(100.0 / (100.0 - effectiveRoll));
    }

    private static int GetStat(IReadOnlyDictionary<string, int> stats, string statId) =>
        stats.TryGetValue(statId, out var value)
            ? Math.Clamp(value, 1, 5)
            : 3;

    private static double StatMultiplier(int stat) => stat switch
    {
        <= 1 => 0.85,
        2 => 0.93,
        3 => 1.00,
        4 => 1.08,
        _ => 1.16
    };

    private static double Interpolate(
        int year,
        int startYear,
        int endYear,
        double startValue,
        double endValue)
    {
        var progress = (year - startYear) / (double)(endYear - startYear);
        return startValue + (endValue - startValue) * Math.Clamp(progress, 0, 1);
    }
}

public sealed record CraftMasteryRule(
    int Level,
    string DisplayName,
    int MasteryBonus,
    double RequiredMasteryProgress,
    int MinimumRelevantExperienceYears,
    decimal ExpectedAnnualIncome);
