using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal static class CareerAptitude
{
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

    public static string GetDisplayName(
        CareerDefinition career,
        IPerson person,
        IStatsService stats)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(stats);

        var names = stats.GetStats(person)
            .ToDictionary(
                stat => stat.Id,
                stat => stat.Name,
                StringComparer.OrdinalIgnoreCase);

        var primary = ResolveDisplay(
            career.PrimaryStat,
            names);

        return string.IsNullOrWhiteSpace(career.SecondaryStat)
            ? primary
            : $"{primary} / "
              + ResolveDisplay(
                  career.SecondaryStat,
                  names);
    }

    private static string ResolveDisplay(
        string statId,
        IReadOnlyDictionary<string, string> names) =>
        names.TryGetValue(statId, out var value)
            ? value
            : statId;
}

internal sealed record EmploymentOpportunity(
    CareerDefinition Career,
    string PrimaryStatId,
    double Ability,
    double SuccessChance);
