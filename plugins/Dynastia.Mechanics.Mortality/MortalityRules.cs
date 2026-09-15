namespace Dynastia.Mechanics.Mortality;

public static class MortalityRules
{
    public const double RandomMortalityScale = 0.40;
    public const double GenericAccidentChance = 0.0008;

    public const int NaturalDeathStartAge = 30;
    public const int NaturalLifespanBase = 40;
    public const int NaturalLifespanPerLongevity = 12;
    public const double NaturalDeathChanceAtProfileAge = 0.08;
    public const double NaturalDeathCurveYears = 8.0;
    public const double MaximumNaturalDeathChance = 0.95;

    public static double ScaleRandomMortality(double chance) =>
        Math.Clamp(chance * RandomMortalityScale, 0, 1);

    public static int GetLongevityProfileAge(int longevity) =>
        NaturalLifespanBase
        + Math.Clamp(longevity, 1, 5)
            * NaturalLifespanPerLongevity;

    public static double GetNaturalDeathChance(
        int age,
        int longevity,
        int immunity)
    {
        if (age <= NaturalDeathStartAge)
            return 0;

        var profileAge =
            GetLongevityProfileAge(longevity);

        // The longevity profile age is the centre of the natural-lifespan
        // curve: annual natural mortality is 8% there, materially lower
        // before it and rapidly higher after it. This keeps Longevity as the
        // dominant determinant of old-age death instead of allowing high
        // Immunity to push ordinary Longevity profiles decades past their
        // intended lifespan.
        var ageCurve =
            Math.Exp(
                (age - profileAge)
                / NaturalDeathCurveYears);

        var immunityAdjustment =
            1.0
            + (3 - Math.Clamp(immunity, 1, 5))
                * 0.05;

        return Math.Clamp(
            NaturalDeathChanceAtProfileAge
            * ageCurve
            * immunityAdjustment,
            0,
            MaximumNaturalDeathChance);
    }
}
