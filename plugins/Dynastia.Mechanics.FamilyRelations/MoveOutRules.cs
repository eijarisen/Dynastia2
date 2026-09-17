namespace Dynastia.Mechanics.FamilyRelations;

public static class MoveOutRules
{
    public const double BaseRefusalChance = 0.20;
    public const double MinimumRefusalChance = 0.10;
    public const double MaximumRefusalChance = 0.80;

    public static double CalculateRefusalChance(
        string? temperament,
        string? morals)
    {
        var refusal = BaseRefusalChance;

        if (temperament?.Equals(
                "Choleric",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            refusal += 0.20;
        }
        else if (temperament?.Equals(
                     "Melancholic",
                     StringComparison.OrdinalIgnoreCase) == true)
        {
            refusal += 0.15;
        }

        if (morals?.Equals(
                "Evil",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            refusal += 0.20;
        }

        return Math.Clamp(
            refusal,
            MinimumRefusalChance,
            MaximumRefusalChance);
    }
}
