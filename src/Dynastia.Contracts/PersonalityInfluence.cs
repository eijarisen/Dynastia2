namespace Dynastia.Contracts;

public static class PersonalityInfluence
{
    public const double MaximumModifier = 0.25;

    public static double Multiplier(
        IPerson person,
        double melancholic = 0,
        double phlegmatic = 0,
        double sanguine = 0,
        double choleric = 0,
        double good = 0,
        double neutral = 0,
        double evil = 0)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        var modifier = 0.0;

        if (person.Tags.Has(
            "personality.melancholic"))
        {
            modifier += melancholic;
        }
        else if (person.Tags.Has(
            "personality.phlegmatic"))
        {
            modifier += phlegmatic;
        }
        else if (person.Tags.Has(
            "personality.sanguine"))
        {
            modifier += sanguine;
        }
        else if (person.Tags.Has(
            "personality.choleric"))
        {
            modifier += choleric;
        }

        if (person.Tags.Has(
            "morals.good"))
        {
            modifier += good;
        }
        else if (person.Tags.Has(
            "morals.neutral"))
        {
            modifier += neutral;
        }
        else if (person.Tags.Has(
            "morals.evil"))
        {
            modifier += evil;
        }

        modifier =
            Math.Clamp(
                modifier,
                -MaximumModifier,
                MaximumModifier);

        return 1.0 + modifier;
    }

    public static double AdjustProbability(
        double probability,
        IPerson person,
        double melancholic = 0,
        double phlegmatic = 0,
        double sanguine = 0,
        double choleric = 0,
        double good = 0,
        double neutral = 0,
        double evil = 0)
    {
        return Math.Clamp(
            probability * Multiplier(
                person,
                melancholic,
                phlegmatic,
                sanguine,
                choleric,
                good,
                neutral,
                evil),
            0,
            1);
    }

    public static string? GetDisplayName(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        var temperament =
            person.Tags.Has(
                "personality.melancholic")
                ? "Melancholic"
                : person.Tags.Has(
                    "personality.phlegmatic")
                    ? "Phlegmatic"
                    : person.Tags.Has(
                        "personality.sanguine")
                        ? "Sanguine"
                        : person.Tags.Has(
                            "personality.choleric")
                            ? "Choleric"
                            : null;

        var morals =
            person.Tags.Has(
                "morals.good")
                ? "Good"
                : person.Tags.Has(
                    "morals.neutral")
                    ? "Neutral"
                    : person.Tags.Has(
                        "morals.evil")
                        ? "Evil"
                        : null;

        return temperament is null
            || morals is null
                ? null
                : $"{temperament} {morals}";
    }
}
