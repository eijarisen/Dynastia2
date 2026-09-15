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

    public static double GenderMultiplier(
        string preference,
        Sex sex) =>
        preference.ToLowerInvariant() switch
        {
            "male-leaning" => sex == Sex.Male ? 1.6 : 0.75,
            "strongly male-leaning" => sex == Sex.Male ? 2.5 : 0.4,
            "female-leaning" => sex == Sex.Female ? 1.6 : 0.75,
            "strongly female-leaning" => sex == Sex.Female ? 2.5 : 0.4,
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
}
