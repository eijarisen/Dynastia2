using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed partial class AboutTextBuilder
{
    private static string PersonalityDescription(
        IPerson person,
        bool isAlive,
        Pronouns pronouns)
    {
        var display =
            PersonalityInfluence.GetDisplayName(
                person);

        if (display is null)
            return string.Empty;

        var parts =
            display.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        var temperament =
            parts.FirstOrDefault()
            ?? string.Empty;

        var morals =
            parts.Skip(1)
                .FirstOrDefault()
            ?? string.Empty;

        var temperamentText =
            temperament switch
            {
                "Melancholic" when isAlive =>
                    $"{pronouns.Subject} has a melancholic temperament: sensitive and reflective, but prone to emotional swings.",

                "Melancholic" =>
                    $"{pronouns.Subject} had a melancholic temperament: sensitive and reflective, but prone to emotional swings.",

                "Phlegmatic" when isAlive =>
                    $"{pronouns.Subject} has a phlegmatic temperament: calm, steady and rarely inclined toward impulsive decisions.",

                "Phlegmatic" =>
                    $"{pronouns.Subject} had a phlegmatic temperament: calm, steady and rarely inclined toward impulsive decisions.",

                "Sanguine" when isAlive =>
                    $"{pronouns.Subject} has a sanguine temperament: energetic, active and generally optimistic.",

                "Sanguine" =>
                    $"{pronouns.Subject} had a sanguine temperament: energetic, active and generally optimistic.",

                "Choleric" when isAlive =>
                    $"{pronouns.Subject} has a choleric temperament: forceful, ambitious and quick to act.",

                "Choleric" =>
                    $"{pronouns.Subject} had a choleric temperament: forceful, ambitious and quick to act.",

                _ =>
                    string.Empty
            };

        var moralsText =
            morals switch
            {
                "Good" when isAlive =>
                    $"{pronouns.Subject} places considerable value on loyalty, responsibility and helping others.",

                "Good" =>
                    $"{pronouns.Subject} placed considerable value on loyalty, responsibility and helping others.",

                "Neutral" when isAlive =>
                    $"{pronouns.Subject} is pragmatic and neither unusually selfless nor especially unscrupulous.",

                "Neutral" =>
                    $"{pronouns.Subject} was pragmatic and neither unusually selfless nor especially unscrupulous.",

                "Evil" when isAlive =>
                    $"{pronouns.Subject} tends to put {pronouns.PossessiveLower} own interests first and is more willing to cross moral boundaries.",

                "Evil" =>
                    $"{pronouns.Subject} tended to put {pronouns.PossessiveLower} own interests first and was more willing to cross moral boundaries.",

                _ =>
                    string.Empty
            };

        return string.Join(
            " ",
            new[]
            {
                temperamentText,
                moralsText
            }
            .Where(
                value =>
                    !string.IsNullOrWhiteSpace(
                        value)));
    }

    private static string ImmunitySuperpowerDescription(
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (value != 5)
            return string.Empty;

        return isAlive
            ? $"{pronouns.Possessive} exceptional constitution sometimes allows {pronouns.Object} to recover from illnesses that would normally linger."
            : $"{pronouns.Possessive} exceptional constitution sometimes allowed {pronouns.Object} to recover from illnesses that would normally linger.";
    }

    private static string LongevitySuperpowerDescription(
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (value != 5)
            return string.Empty;

        return isAlive
            ? $"{pronouns.Subject} possesses remarkable physical resilience and can occasionally survive even a seemingly fatal collapse."
            : $"{pronouns.Subject} possessed remarkable physical resilience and could occasionally survive even a seemingly fatal collapse.";
    }

    private static string FertilitySuperpowerDescription(
        int value,
        bool isAlive,
        Sex sex,
        Pronouns pronouns)
    {
        if (value != 5
            || sex != Sex.Female)
        {
            return string.Empty;
        }

        return isAlive
            ? $"{pronouns.Possessive} exceptional fertility makes multiple births more likely."
            : $"{pronouns.Possessive} exceptional fertility made multiple births more likely.";
    }

    private static string AppealSuperpowerDescription(
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (value != 5)
            return string.Empty;

        return isAlive
            ? $"{pronouns.Possessive} exceptional attractiveness tends to draw unusually promising partners."
            : $"{pronouns.Possessive} exceptional attractiveness tended to draw unusually promising partners.";
    }

    private static string StrengthSuperpowerDescription(
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (value != 5)
            return string.Empty;

        return isAlive
            ? $"{pronouns.Possessive} exceptional physical resilience also gives {pronouns.Object} a stabilizing presence in family life."
            : $"{pronouns.Possessive} exceptional physical resilience also gave {pronouns.Object} a stabilizing presence in family life.";
    }

    private static string IntellectSuperpowerDescription(
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (value != 5)
            return string.Empty;

        return isAlive
            ? $"{pronouns.Possessive} exceptional intellect helps {pronouns.Object} manage household resources with unusual efficiency."
            : $"{pronouns.Possessive} exceptional intellect helped {pronouns.Object} manage household resources with unusual efficiency.";
    }

    private static string ImmunityDescription(
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (isAlive)
        {
            return value switch
            {
                <= 2 =>
                    $"{pronouns.Subject} has a delicate constitution, " +
                    $"making {pronouns.Object} more prone to sickness.",

                >= 4 =>
                    $"{pronouns.Subject} has a robust constitution and " +
                    "strong resistance to illness.",

                _ =>
                    $"{pronouns.Subject} has a generally healthy " +
                    "constitution with ordinary resistance to illness."
            };
        }

        return value switch
        {
            <= 2 =>
                $"{pronouns.Subject} had a delicate constitution, " +
                $"which made {pronouns.Object} more prone to sickness.",

            >= 4 =>
                $"{pronouns.Subject} had a robust constitution and " +
                "was strongly resistant to illness.",

            _ =>
                $"{pronouns.Subject} had a generally healthy constitution " +
                "with ordinary resistance to illness."
        };
    }

    private static string LongevityDescription(
        IPerson person,
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (isAlive)
        {
            return value switch
            {
                1 =>
                    $"{pronouns.Possessive} constitution suggests relatively " +
                    "poor longevity and a greater likelihood of a shortened life.",

                2 =>
                    $"{pronouns.Possessive} constitution suggests somewhat " +
                    "limited longevity.",

                3 =>
                    $"{pronouns.Possessive} constitution suggests ordinary " +
                    "longevity for the period.",

                4 =>
                    $"{pronouns.Possessive} constitution suggests strong " +
                    "longevity and a good prospect of living to an advanced age.",

                _ =>
                    $"{pronouns.Possessive} constitution suggests exceptional " +
                    "longevity and an unusually strong prospect of a very long life."
            };
        }

        var expectedAge =
            BaseLifespan
            + value
                * LongevityMultiplier;

        var difference =
            person.Age
            - expectedAge;

        var profile =
            value switch
            {
                1 =>
                    "a constitution associated with poor longevity",

                2 =>
                    "a constitution associated with somewhat limited longevity",

                3 =>
                    "a constitution associated with ordinary longevity",

                4 =>
                    "a constitution associated with strong longevity",

                _ =>
                    "a constitution associated with exceptional longevity"
            };

        if (Math.Abs(
                difference)
            <= 3)
        {
            return
                $"{pronouns.Subject} had {profile}, and " +
                $"{pronouns.PossessiveLower} eventual lifespan was " +
                "broadly consistent with that natural disposition.";
        }

        if (difference >= 12)
        {
            return
                $"{pronouns.Subject} had {profile}, yet " +
                $"{pronouns.SubjectLower} lived far longer than " +
                "that natural disposition would normally suggest.";
        }

        if (difference > 3)
        {
            return
                $"{pronouns.Subject} had {profile}, yet " +
                $"{pronouns.SubjectLower} outlived what that " +
                "natural disposition would normally suggest.";
        }

        if (difference <= -12)
        {
            return
                $"{pronouns.Subject} had {profile}, but " +
                $"{pronouns.SubjectLower} died much earlier than " +
                "that natural disposition would normally suggest.";
        }

        return
            $"{pronouns.Subject} had {profile}, but " +
            $"{pronouns.SubjectLower} died earlier than " +
            "that natural disposition would normally suggest.";
    }

}
