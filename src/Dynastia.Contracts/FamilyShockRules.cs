namespace Dynastia.Contracts;

public static class FamilyShockRules
{
    private const double SameHouseholdMultiplier = 1.25;
    private const double ExtendedRelativeMultiplier = 0.70;

    public static double ScaleHealthLoss(
        IPerson person,
        double baseLoss,
        bool sameHousehold,
        bool extendedRelative)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        var relationshipMultiplier =
            extendedRelative
                ? ExtendedRelativeMultiplier
                : 1.0;

        var householdMultiplier =
            sameHousehold
                ? SameHouseholdMultiplier
                : 1.0;

        var personalityMultiplier =
            PersonalityInfluence.Multiplier(
                person,
                melancholic: 0.20,
                phlegmatic: -0.20,
                sanguine: 0.0,
                choleric: 0.20,
                good: 0.15,
                neutral: 0.0,
                evil: -0.15);

        return baseLoss
            * relationshipMultiplier
            * householdMultiplier
            * personalityMultiplier;
    }
}
