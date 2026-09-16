namespace Dynastia.Mechanics.Career;

/// <summary>
/// Starting/generated adult career levels. Level 3 is deliberately uncommon
/// and requires at least some education; young adults are much less likely to
/// begin already established than older adults with a pre-game work history.
/// </summary>
public static class InitialCareerProfileRules
{
    public static int ResolveJobLevel(
        int age,
        int educationLevel,
        double roll)
    {
        if (age < 18)
            return 0;

        var education = Math.Clamp(educationLevel, 0, 5);
        var youngAdult = age < 25;
        var sample = Math.Clamp(roll, 0, 0.999999999999);

        var (unemployedChance, levelTwoChance, levelThreeChance) =
            youngAdult
                ? education switch
                {
                    0 => (0.30, 0.05, 0.00),
                    1 => (0.25, 0.19, 0.01),
                    2 => (0.20, 0.28, 0.02),
                    _ => (0.15, 0.36, 0.04)
                }
                : education switch
                {
                    0 => (0.20, 0.20, 0.00),
                    1 => (0.15, 0.32, 0.03),
                    2 => (0.12, 0.37, 0.06),
                    _ => (0.10, 0.40, 0.10)
                };

        if (sample < unemployedChance)
            return 0;

        var levelThreeStart = 1.0 - levelThreeChance;
        if (sample >= levelThreeStart)
            return 3;

        var levelTwoStart = levelThreeStart - levelTwoChance;
        if (sample >= levelTwoStart)
            return 2;

        return 1;
    }
}
