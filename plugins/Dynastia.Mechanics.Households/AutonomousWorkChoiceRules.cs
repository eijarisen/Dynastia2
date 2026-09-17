namespace Dynastia.Mechanics.Households;

public static class AutonomousWorkChoiceRules
{
    public const decimal SwitchingPremium = 0.05m;

    public static decimal GetCareerOpportunityValue(
        decimal annualSalary,
        double successChance,
        bool urgent)
    {
        var salary = Math.Max(0m, annualSalary);
        var chance = (decimal)Math.Clamp(successChance, 0.0, 1.0);

        // Poor households care more about the chance of money arriving this
        // year. Stable households can value the persistence of a successful
        // career more heavily without pretending a low-probability vacancy is
        // equivalent to guaranteed income.
        var realization = urgent
            ? chance
            : 0.55m + 0.45m * chance;

        return salary * realization;
    }

    public static bool IsMateriallyBetter(
        decimal candidateValue,
        decimal alternativeValue,
        decimal premium = SwitchingPremium)
    {
        var candidate = Math.Max(0m, candidateValue);
        var alternative = Math.Max(0m, alternativeValue);

        if (alternative <= 0m)
            return candidate > 0m;

        return candidate >= alternative * (1m + Math.Max(0m, premium));
    }

    public static double GetAdvantageBonus(
        decimal preferredValue,
        decimal alternativeValue,
        double maximumBonus = 18.0)
    {
        var preferred = Math.Max(0m, preferredValue);
        var alternative = Math.Max(0m, alternativeValue);
        if (preferred <= alternative || preferred <= 0m)
            return 0;

        if (alternative <= 0m)
            return maximumBonus;

        var relativeGain = (double)((preferred - alternative) / alternative);
        return Math.Clamp(relativeGain * 30.0, 0, maximumBonus);
    }
}
