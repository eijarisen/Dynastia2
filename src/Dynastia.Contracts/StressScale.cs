namespace Dynastia.Contracts;

public static class StressScale
{
    public const double Multiplier = 10.0;
    public const double LegacyMaximum = 10.0;
    public const double Maximum = LegacyMaximum * Multiplier;

    public static double FromLegacy(double value) =>
        value * Multiplier;

    public static double ToLegacy(double value) =>
        value / Multiplier;
}
