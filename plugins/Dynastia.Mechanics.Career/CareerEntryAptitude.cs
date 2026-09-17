namespace Dynastia.Mechanics.Career;

internal static class CareerAptitude
{
    private static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["strength"] = "Strength",
            ["intellect"] = "Intellect",
            ["appeal"] = "Appeal"
        };

    public static double GetComposite(
        CareerDefinition career,
        Func<string, int> getStat)
    {
        return CareerBalanceRules.GetCompositeAptitude(
            getStat(career.PrimaryStat),
            string.IsNullOrWhiteSpace(career.SecondaryStat)
                ? null
                : getStat(career.SecondaryStat));
    }

    public static string GetDisplayName(CareerDefinition career)
    {
        var primary = ResolveDisplay(career.PrimaryStat);
        return string.IsNullOrWhiteSpace(career.SecondaryStat)
            ? primary
            : $"{primary} / {ResolveDisplay(career.SecondaryStat)}";
    }

    private static string ResolveDisplay(string statId) =>
        DisplayNames.TryGetValue(statId, out var value) ? value : statId;
}

internal sealed record EmploymentOpportunity(
    CareerDefinition Career,
    string PrimaryStatId,
    double Ability,
    double SuccessChance);
