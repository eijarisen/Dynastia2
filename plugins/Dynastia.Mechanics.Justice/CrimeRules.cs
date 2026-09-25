using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public static class CrimeRules
{
    public static double CalculateAttemptChance(
        CrimeAttemptRules rules,
        double contextMultiplier,
        bool broke,
        double stress)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var stressMultiplier = Math.Min(
            rules.MaximumStressMultiplier,
            1.0 + StressScale.ToLegacy(Math.Max(0, stress)) * rules.StressMultiplierPerPoint);
        var chance = rules.BaseAttemptChance
            * Math.Max(0, contextMultiplier)
            * (broke ? rules.PovertyMultiplier : 1.0)
            * stressMultiplier;
        return Math.Clamp(
            chance,
            rules.MinimumAttemptChance,
            rules.MaximumAttemptChance);
    }

    public static double CalculateAptitude(
        CrimeDefinition crime,
        Func<string, int> getStat)
    {
        ArgumentNullException.ThrowIfNull(crime);
        ArgumentNullException.ThrowIfNull(getStat);
        var primary = Math.Clamp(getStat(crime.PrimaryStat), 1, 5);
        if (string.IsNullOrWhiteSpace(crime.SecondaryStat))
            return primary;
        var secondary = Math.Clamp(getStat(crime.SecondaryStat), 1, 5);
        return primary * 0.75 + secondary * 0.25;
    }

    public static double CalculateProfitSuccessChance(
        CrimeDefinition crime,
        double aptitude)
    {
        if (!crime.IsProfitCrime)
            return 1;
        return Math.Clamp(
            crime.SuccessBase + (Math.Clamp(aptitude, 1, 5) - 3) * 0.06,
            0.10,
            0.95);
    }

    public static double CalculateDetectionChance(
        CrimeDefinition crime,
        int intellect)
    {
        var effect = crime.IsPlanned
            ? (3 - Math.Clamp(intellect, 1, 5)) * 0.05
            : (3 - Math.Clamp(intellect, 1, 5)) * 0.01;
        return Math.Clamp(crime.DetectionBase + effect, 0.10, 0.98);
    }

    public static double CalculateAptitudeSelectionMultiplier(double aptitude) =>
        Math.Clamp(1.0 + (Math.Clamp(aptitude, 1, 5) - 3) * 0.15, 0.55, 1.45);

    public static double CalculateStressSelectionMultiplier(
        CrimeDefinition crime,
        double stress) =>
        Math.Max(0.1, 1.0 + StressScale.ToLegacy(Math.Max(0, stress)) * crime.StressWeightPerPoint);
}
