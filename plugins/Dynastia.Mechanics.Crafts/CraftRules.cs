using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

public static class CraftRules
{
    public const int MaximumCrafts = 2;
    public const double PassiveLearningChance = 0.05;

    public static double GetTeachingSuccessChance(int childIntellect) =>
        Math.Clamp(0.40 + Math.Clamp(childIntellect, 1, 5) * 0.05, 0, 1);

    public static double GetApplicationBonus(
        IEnumerable<CraftInfo> knownCrafts,
        string careerId)
    {
        ArgumentNullException.ThrowIfNull(knownCrafts);
        ArgumentException.ThrowIfNullOrWhiteSpace(careerId);

        var best = 0.0;
        foreach (var craft in knownCrafts)
        {
            if (craft.PrimaryCareerId.Equals(careerId, StringComparison.OrdinalIgnoreCase))
                best = Math.Max(best, 0.15);
            else if (craft.RelatedCareerIds.Contains(careerId, StringComparer.OrdinalIgnoreCase))
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
