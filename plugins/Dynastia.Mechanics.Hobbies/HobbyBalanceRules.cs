using Dynastia.Contracts;

namespace Dynastia.Mechanics.Hobbies;

public static class HobbyBalanceRules
{
    public const int MaximumHobbies = 2;
    public const double AnnualAcquisitionChance = 0.02;
    public const double HouseholdInfluenceMultiplier = 3.0;
    public const int ThoughtSalience = 8;

    public static double TownMultiplier(
        string preference,
        SettlementClass settlementClass) =>
        preference.ToLowerInvariant() switch
        {
            "rural" => settlementClass switch
            {
                SettlementClass.SmallTown => 1.7,
                SettlementClass.Town => 1.3,
                SettlementClass.City => 0.8,
                SettlementClass.MajorCity => 0.6,
                _ => 1.0
            },
            "urban" => settlementClass switch
            {
                SettlementClass.SmallTown => 0.6,
                SettlementClass.Town => 0.9,
                SettlementClass.City => 1.4,
                SettlementClass.MajorCity => 1.7,
                _ => 1.0
            },
            _ => 1.0
        };

    public static double TemperamentMultiplier(
        HobbyDefinition hobby,
        string? temperament)
    {
        if (string.IsNullOrWhiteSpace(temperament))
            return 1.0;

        if (hobby.PrimaryTemperament.Equals(
                temperament,
                StringComparison.OrdinalIgnoreCase))
        {
            return 1.7;
        }

        if (hobby.SecondaryTemperament?.Equals(
                temperament,
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return 1.35;
        }

        return 1.0;
    }

    public static double StatMultiplier(
        HobbyDefinition hobby,
        IReadOnlyDictionary<string, int> stats)
    {
        ArgumentNullException.ThrowIfNull(hobby);
        ArgumentNullException.ThrowIfNull(stats);

        var primary = ResolveStatMultiplier(GetStat(stats, hobby.PrimaryStat));
        if (string.IsNullOrWhiteSpace(hobby.SecondaryStat))
            return primary;

        var secondaryBase = ResolveStatMultiplier(GetStat(stats, hobby.SecondaryStat));
        var secondary = 1.0 + (secondaryBase - 1.0) * 0.5;
        return primary * secondary;
    }

    private static int GetStat(
        IReadOnlyDictionary<string, int> stats,
        string statId) =>
        stats.TryGetValue(statId, out var value)
            ? Math.Clamp(value, 1, 5)
            : 3;

    private static double ResolveStatMultiplier(int stat) => stat switch
    {
        <= 1 => 0.85,
        2 => 0.93,
        3 => 1.00,
        4 => 1.08,
        _ => 1.16
    };
}
