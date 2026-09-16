namespace Dynastia.Mechanics.Relationships;

/// <summary>
/// Deterministic profile rules for generated marriage candidates. These keep
/// education and career standing correlated with the candidate's abilities
/// instead of rolling each field independently.
/// </summary>
public static class PartnerCandidateProfileRules
{
    public static int ResolveEducationLevel(
        int minimumLevel,
        int maximumLevel,
        int intellect,
        double roll)
    {
        var minimum = Math.Clamp(minimumLevel, 0, 5);
        var maximum = Math.Clamp(maximumLevel, minimum, 5);
        if (maximum == minimum)
            return minimum;

        var intellectPosition =
            (Math.Clamp(intellect, 1, 5) - 1) / 4.0;
        var noise =
            (Math.Clamp(roll, 0, 0.999999999999) - 0.5) * 0.50;
        var position = Math.Clamp(
            0.15 + intellectPosition * 0.70 + noise,
            0,
            1);

        var span = maximum - minimum;
        return minimum + (int)Math.Round(
            position * span,
            MidpointRounding.AwayFromZero);
    }

    public static int ResolveDesiredJobLevel(
        int age,
        int educationLevel,
        int strength,
        int intellect,
        double roll)
    {
        if (age < 18)
            return 0;

        var education = Math.Clamp(educationLevel, 0, 5);
        var aptitude = Math.Max(
            Math.Clamp(strength, 1, 5),
            Math.Clamp(intellect, 1, 5));
        var youngAdult = age < 25;
        var sample = Math.Clamp(roll, 0, 0.999999999999);

        var unemploymentChance = youngAdult
            ? 0.34 - education * 0.035 - (aptitude - 1) * 0.025
            : 0.22 - education * 0.025 - (aptitude - 1) * 0.02;
        unemploymentChance = Math.Clamp(
            unemploymentChance,
            youngAdult ? 0.12 : 0.07,
            youngAdult ? 0.34 : 0.22);

        var levelThreeChance = youngAdult
            ? 0.005
                + Math.Max(0, education - 1) * 0.008
                + Math.Max(0, aptitude - 2) * 0.008
            : 0.015
                + education * 0.012
                + Math.Max(0, aptitude - 2) * 0.012;
        levelThreeChance = Math.Clamp(
            levelThreeChance,
            0,
            youngAdult ? 0.06 : 0.14);

        var levelTwoChance = youngAdult
            ? 0.13 + education * 0.045 + (aptitude - 1) * 0.025
            : 0.24 + education * 0.04 + (aptitude - 1) * 0.025;
        levelTwoChance = Math.Clamp(
            levelTwoChance,
            0.10,
            youngAdult ? 0.42 : 0.52);

        if (sample < unemploymentChance)
            return 0;

        if (sample >= 1.0 - levelThreeChance)
            return 3;

        if (sample >= 1.0 - levelThreeChance - levelTwoChance)
            return 2;

        return 1;
    }
}
