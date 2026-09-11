using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed class AboutTextBuilder
{
    private const int AdultAge =
        18;

    private const int ElderAge =
        60;

    private const int YoungChildAge =
        10;

    // Used only internally to compare a deceased person's actual lifespan
    // with their Longevity profile. The numeric estimate is never exposed
    // in About text.
    private const int BaseLifespan =
        40;

    private const int LongevityMultiplier =
        12;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly ILocationService _locations;

    public AboutTextBuilder(
        IGameState gameState,
        IFamilyService family,
        IStatsService stats,
        ILocationService locations)
    {
        _gameState =
            gameState;

        _family =
            family;

        _stats =
            stats;

        _locations =
            locations;
    }

    public string Build(
        IPerson person)
    {
        var stats =
            _stats.GetStats(
                person)
            .ToDictionary(
                stat =>
                    stat.Id,
                stat =>
                    stat.Value,
                StringComparer.OrdinalIgnoreCase);

        var sex =
            _family.GetSex(
                person);

        var pronouns =
            Pronouns.For(
                sex);

        var isAlive =
            person.Tags.Has(
                "state.alive")
            && !person.Tags.Has(
                "state.dead");

        var location =
            _locations.GetLocation(
                person);

        var sentences =
            new List<string>
            {
                OpeningDescription(
                    person,
                    location,
                    pronouns),

                ImmunityDescription(
                    stats["immunity"],
                    isAlive,
                    pronouns),

                LongevityDescription(
                    person,
                    stats["longevity"],
                    isAlive,
                    pronouns),

                FertilityDescription(
                    person,
                    stats["fertility"],
                    isAlive,
                    sex,
                    pronouns),

                AppealDescription(
                    person,
                    stats["appeal"],
                    isAlive,
                    pronouns),

                StrengthDescription(
                    person,
                    stats["strength"],
                    isAlive,
                    pronouns),

                IntellectDescription(
                    stats["intellect"],
                    isAlive,
                    pronouns)
            };

        return string.Join(
            " ",
            sentences
                .Where(
                    sentence =>
                        !string.IsNullOrWhiteSpace(
                            sentence))
                .Select(
                    sentence =>
                        sentence.Trim()));
    }

    private string OpeningDescription(
        IPerson person,
        LocationSnapshot location,
        Pronouns pronouns)
    {
        var birthDate =
            person.BirthDate?.ToString()
            ?? "an unknown date";

        var opening =
            $"{_family.GetDisplayName(person)} " +
            $"was born on {birthDate} in " +
            $"{location.Birthplace.DisplayName}";

        var parents =
            GetKnownParents(
                person);

        if (parents is not null)
        {
            opening +=
                $" to {parents.Value.Father} " +
                $"and {parents.Value.Mother}";
        }

        opening +=
            GetBirthOrderText(
                person);

        opening +=
            ".";

        if (!person.Tags.Has(
                "state.dead"))
        {
            return opening;
        }

        var deathTown =
            location.DeathTown
            ?? location.HomeTown;

        if (person.DeathDate
            is GameDate deathDate)
        {
            opening +=
                $" {pronouns.Subject} died on " +
                $"{deathDate} in " +
                $"{deathTown.DisplayName} at age " +
                $"{person.Age}.";
        }
        else
        {
            opening +=
                $" {pronouns.Subject} died in " +
                $"{deathTown.DisplayName} at age " +
                $"{person.Age}.";
        }

        return opening;
    }

    private (
        string Father,
        string Mother)?
        GetKnownParents(
            IPerson person)
    {
        var father =
            _family.GetFather(
                person);

        var mother =
            _family.GetMother(
                person);

        if (father is not null
            && mother is not null)
        {
            return (
                _family.GetDisplayName(
                    father),
                _family.GetDisplayName(
                    mother));
        }

        var background =
            _family.GetGeneratedFamilyBackground(
                person);

        if (background is null)
            return null;

        var fatherName =
            NormalizeKnownName(
                background.FatherName);

        var motherName =
            NormalizeKnownName(
                background.MotherName);

        // If both parents are not genuinely known, omit the parent phrase
        // entirely instead of writing "unknown and unknown".
        if (fatherName is null
            || motherName is null)
        {
            return null;
        }

        return (
            fatherName,
            motherName);
    }

    private static string? NormalizeKnownName(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var trimmed =
            value.Trim();

        return trimmed.Equals(
                "unknown",
                StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed;
    }

    private string GetBirthOrderText(
        IPerson person)
    {
        var father =
            _family.GetFather(
                person);

        if (father is null)
            return string.Empty;

        var siblings =
            _family.GetChildren(
                father)
            .OrderBy(
                child =>
                    child.BirthDate?.Year
                    ?? int.MaxValue)
            .ThenBy(
                child =>
                    child.BirthDate?.Month
                    ?? int.MaxValue)
            .ThenBy(
                child =>
                    child.BirthDate?.Day
                    ?? int.MaxValue)
            .ThenBy(
                child =>
                    _gameState.People
                        .ToList()
                        .IndexOf(
                            child))
            .ToList();

        var index =
            siblings.FindIndex(
                sibling =>
                    sibling.Id
                    == person.Id);

        return index < 0
            ? string.Empty
            : $" as their " +
              $"{ToOrdinal(index + 1)} child";
    }

    private static string ToOrdinal(
        int value)
    {
        var mod100 =
            value % 100;

        if (mod100 is 11 or 12 or 13)
            return $"{value}th";

        return (value % 10) switch
        {
            1 => $"{value}st",
            2 => $"{value}nd",
            3 => $"{value}rd",
            _ => $"{value}th"
        };
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

    private string FertilityDescription(
        IPerson person,
        int value,
        bool isAlive,
        Sex sex,
        Pronouns pronouns)
    {
        if (!isAlive
            && person.Age < AdultAge)
        {
            return
                $"{pronouns.Subject} died before adulthood, so " +
                $"{pronouns.PossessiveLower} fertility potential was never " +
                "meaningfully expressed.";
        }

        var expectation =
            FertilityExpectation.For(
                value);

        var completedFertility =
            !isAlive
            || (
                sex == Sex.Female
                && person.Age >= 45
            );

        if (!completedFertility)
        {
            return
                $"{pronouns.Possessive} fertility profile suggests " +
                $"{expectation.LivingDescription}.";
        }

        var children =
            _family.GetChildren(
                person)
            .Count;

        var actualText =
            children == 0
                ? $"{pronouns.Subject} ultimately had no children"
                : $"{pronouns.Subject} ultimately had " +
                  $"{ChildCountText(children)}";

        var comparison =
            CompareChildrenToExpectation(
                children,
                expectation);

        return
            $"{pronouns.Possessive} fertility profile suggested " +
            $"{expectation.CompletedDescription}; " +
            $"{actualText}, {comparison}.";
    }

    private static string AppealDescription(
        IPerson person,
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (person.Age < AdultAge)
        {
            if (isAlive)
            {
                return value switch
                {
                    1 =>
                        $"As a child, {pronouns.PossessiveLower} appearance " +
                        "is still developing, though " +
                        $"{pronouns.SubjectLower} currently has rather plain features.",

                    2 =>
                        $"As a child, {pronouns.PossessiveLower} appearance " +
                        "is still developing and is presently unremarkable.",

                    3 =>
                        $"As a child, {pronouns.PossessiveLower} appearance " +
                        "is still developing and is presently pleasant and ordinary.",

                    4 =>
                        $"As a child, {pronouns.SubjectLower} is already " +
                        "notably attractive for the age.",

                    _ =>
                        $"As a child, {pronouns.SubjectLower} already has " +
                        "unusually striking features."
                };
            }

            return value switch
            {
                1 =>
                    $"As a child, {pronouns.SubjectLower} had rather plain features.",

                2 =>
                    $"As a child, {pronouns.SubjectLower} had an unremarkable appearance.",

                3 =>
                    $"As a child, {pronouns.SubjectLower} had a pleasant, ordinary appearance.",

                4 =>
                    $"As a child, {pronouns.SubjectLower} was notably attractive for the age.",

                _ =>
                    $"As a child, {pronouns.SubjectLower} had unusually striking features."
            };
        }

        if (person.Age >= ElderAge)
        {
            if (isAlive)
            {
                return value switch
                {
                    <= 2 =>
                        $"In later life, {pronouns.SubjectLower} has a fairly " +
                        "plain appearance and modest social presence.",

                    3 =>
                        $"In later life, {pronouns.SubjectLower} has an ordinary " +
                        "but pleasant appearance.",

                    4 =>
                        $"In later life, {pronouns.SubjectLower} retains a " +
                        "distinguished and attractive appearance.",

                    _ =>
                        $"In later life, {pronouns.SubjectLower} retains a " +
                        "remarkably striking and charismatic presence."
                };
            }

            return value switch
            {
                <= 2 =>
                    $"In later life, {pronouns.SubjectLower} had a fairly " +
                    "plain appearance and modest social presence.",

                3 =>
                    $"In later life, {pronouns.SubjectLower} had an ordinary " +
                    "but pleasant appearance.",

                4 =>
                    $"In later life, {pronouns.SubjectLower} retained a " +
                    "distinguished and attractive appearance.",

                _ =>
                    $"In later life, {pronouns.SubjectLower} retained a " +
                    "remarkably striking and charismatic presence."
            };
        }

        if (isAlive)
        {
            return value switch
            {
                1 =>
                    $"{pronouns.Subject} is considered very unattractive.",

                2 =>
                    $"{pronouns.Subject} is not particularly visually appealing.",

                3 =>
                    $"{pronouns.Subject} has a pleasant and modest appearance.",

                4 =>
                    $"{pronouns.Subject} is regarded as good-looking.",

                _ =>
                    $"{pronouns.Subject} has a striking and highly attractive presence."
            };
        }

        return value switch
        {
            1 =>
                $"{pronouns.Subject} was considered very unattractive.",

            2 =>
                $"{pronouns.Subject} was not considered particularly visually appealing.",

            3 =>
                $"{pronouns.Subject} had a pleasant and modest appearance.",

            4 =>
                $"{pronouns.Subject} was regarded as good-looking.",

            _ =>
                $"{pronouns.Subject} had a striking and highly attractive presence."
        };
    }

    private static string StrengthDescription(
        IPerson person,
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (isAlive
            && person.Age < YoungChildAge)
        {
            return value switch
            {
                <= 2 =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggests below-average strength as " +
                    $"{pronouns.SubjectLower} grows.",

                3 =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggests average strength as " +
                    $"{pronouns.SubjectLower} grows.",

                _ =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggests an unusually strong build as " +
                    $"{pronouns.SubjectLower} grows."
            };
        }

        if (!isAlive
            && person.Age < YoungChildAge)
        {
            return value switch
            {
                <= 2 =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggested relatively weak future strength.",

                3 =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggested average future strength.",

                _ =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggested unusually strong future physical development."
            };
        }

        if (person.Age < AdultAge)
        {
            if (isAlive)
            {
                return value switch
                {
                    <= 2 =>
                        $"Physically, {pronouns.SubjectLower} is relatively weak for the age.",

                    3 =>
                        $"Physically, {pronouns.SubjectLower} has average strength for the age.",

                    _ =>
                        $"Physically, {pronouns.SubjectLower} is notably strong for the age."
                };
            }

            return value switch
            {
                <= 2 =>
                    $"Physically, {pronouns.SubjectLower} was relatively weak for the age.",

                3 =>
                    $"Physically, {pronouns.SubjectLower} had average strength for the age.",

                _ =>
                    $"Physically, {pronouns.SubjectLower} was notably strong for the age."
            };
        }

        if (isAlive)
        {
            return value switch
            {
                <= 2 =>
                    $"Physically, {pronouns.SubjectLower} is weak and " +
                    "not especially capable of demanding exertion.",

                3 =>
                    $"Physically, {pronouns.SubjectLower} has average strength.",

                _ =>
                    $"Physically, {pronouns.SubjectLower} is notably strong " +
                    "and capable of demanding exertion."
            };
        }

        return value switch
        {
            <= 2 =>
                $"Physically, {pronouns.SubjectLower} was weak and " +
                "not especially capable of demanding exertion.",

            3 =>
                $"Physically, {pronouns.SubjectLower} had average strength.",

            _ =>
                $"Physically, {pronouns.SubjectLower} was notably strong " +
                "and capable of demanding exertion."
        };
    }

    private static string IntellectDescription(
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (isAlive)
        {
            return value switch
            {
                1 =>
                    $"Mentally, {pronouns.SubjectLower} has very limited intellectual ability.",

                2 =>
                    $"Mentally, {pronouns.SubjectLower} is somewhat below average in intellect.",

                3 =>
                    $"Mentally, {pronouns.SubjectLower} has an average intellect.",

                4 =>
                    $"Mentally, {pronouns.SubjectLower} is bright and quick to understand difficult matters.",

                _ =>
                    $"Mentally, {pronouns.SubjectLower} is exceptionally gifted, with a remarkably sharp intellect."
            };
        }

        return value switch
        {
            1 =>
                $"Mentally, {pronouns.SubjectLower} had very limited intellectual ability.",

            2 =>
                $"Mentally, {pronouns.SubjectLower} was somewhat below average in intellect.",

            3 =>
                $"Mentally, {pronouns.SubjectLower} had an average intellect.",

            4 =>
                $"Mentally, {pronouns.SubjectLower} was bright and quick to understand difficult matters.",

            _ =>
                $"Mentally, {pronouns.SubjectLower} was exceptionally gifted, with a remarkably sharp intellect."
        };
    }

    private static string ChildCountText(
        int value)
    {
        return value == 1
            ? "1 child"
            : $"{value} children";
    }

    private static string CompareChildrenToExpectation(
        int actual,
        FertilityExpectation expectation)
    {
        if (actual < expectation.Minimum)
        {
            return
                "fewer than the fertility profile would normally suggest";
        }

        if (expectation.Maximum
                is int maximum
            && actual > maximum)
        {
            return
                "more than the fertility profile would normally suggest";
        }

        return
            "a family outcome broadly consistent with that fertility profile";
    }

    private sealed record FertilityExpectation(
        string LivingDescription,
        string CompletedDescription,
        int Minimum,
        int? Maximum)
    {
        public static FertilityExpectation For(
            int value)
        {
            return value switch
            {
                <= 0 =>
                    new FertilityExpectation(
                        "very poor fertility and little realistic prospect of parenthood",
                        "very poor fertility with little natural prospect of parenthood",
                        0,
                        0),

                1 =>
                    new FertilityExpectation(
                        "low fertility and a tendency toward either childlessness or a very small family",
                        "low fertility and a tendency toward a very small family",
                        0,
                        1),

                2 =>
                    new FertilityExpectation(
                        "below-average fertility and a tendency toward a smaller family",
                        "below-average fertility and a tendency toward a smaller family",
                        1,
                        2),

                3 =>
                    new FertilityExpectation(
                        "ordinary fertility and a moderate prospect of parenthood",
                        "ordinary fertility and a moderate family tendency",
                        2,
                        3),

                4 =>
                    new FertilityExpectation(
                        "high fertility and a strong tendency toward a larger family",
                        "high fertility and a strong tendency toward a larger family",
                        3,
                        4),

                _ =>
                    new FertilityExpectation(
                        "exceptional fertility and a strong natural tendency toward a large family",
                        "exceptional fertility and a strong natural tendency toward a large family",
                        4,
                        null)
            };
        }
    }

    private sealed record Pronouns(
        string Subject,
        string SubjectLower,
        string Possessive,
        string PossessiveLower,
        string Object)
    {
        public static Pronouns For(
            Sex sex)
        {
            return sex == Sex.Male
                ? new Pronouns(
                    "He",
                    "he",
                    "His",
                    "his",
                    "him")
                : new Pronouns(
                    "She",
                    "she",
                    "Her",
                    "her",
                    "her");
        }
    }
}
