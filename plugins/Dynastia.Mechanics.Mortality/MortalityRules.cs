namespace Dynastia.Mechanics.Mortality;

public static class MortalityRules
{
    public const double RandomMortalityScale = 0.40;
    public const double GenericAccidentChance = 0.0008;

    public static double ScaleRandomMortality(double chance) =>
        Math.Clamp(chance * RandomMortalityScale, 0, 1);
}
