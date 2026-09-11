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

    // Dynasty 4 natural-lifespan formula.
    private const int BaseLifespan =
        40;

    private const int LongevityMultiplier =
        12;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;

    public AboutTextBuilder(
        IGameState gameState,
        IFamilyService family,
        IStatsService stats)
    {
        _gameState = gameState;
        _family = family;
        _stats = stats;
    }

    public string Build(
        IPerson person)
    {
        var background =
            _family.GetGeneratedFamilyBackground(
                person);

        string fatherName;
        string motherName;

        if (background is not null)
        {
            fatherName =
                background.FatherName;

            motherName =
                background.MotherName;
        }
        else
        {
            fatherName =
                NameOrUnknown(
                    _family.GetFather(
                        person));

            motherName =
                NameOrUnknown(
                    _family.GetMother(
                        person));
        }

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

        var birthDate =
            person.BirthDate?.ToString()
            ?? "an unknown date";

        var opening =
            $"{_family.GetDisplayName(person)} " +
            $"was born on {birthDate} to " +
            $"{fatherName} and {motherName}" +
            $"{GetBirthOrderText(person)}.";

        if (!isAlive)
        {
            opening +=
                $" {pronouns.Subject} died at age " +
                $"{person.Age}.";
        }

        var sections =
            new[]
            {
                opening,

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
            Environment.NewLine
            + Environment.NewLine,
            sections);
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

    private string NameOrUnknown(
        IPerson? person)
    {
        return person is null
            ? "unknown"
            : _family.GetDisplayName(
                person);
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
                    $"{pronouns.Subject} has a robust constitution " +
                    "and strong resistance to illness.",

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
                $"{pronouns.Subject} had a robust constitution " +
                "and was strongly resistant to illness.",

            _ =>
                $"{pronouns.Subject} had a generally healthy " +
                "constitution with ordinary resistance to illness."
        };
    }

    private static string LongevityDescription(
        IPerson person,
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        var expectedAge =
            BaseLifespan
            + value
                * LongevityMultiplier;

        if (isAlive)
        {
            if (person.Age
                > expectedAge)
            {
                return
                    $"{pronouns.Possessive} Longevity profile suggests " +
                    $"a life expectancy of about {expectedAge} years. " +
                    $"At age {person.Age}, {pronouns.SubjectLower} has " +
                    "already outlived that estimate.";
            }

            return
                $"{pronouns.Possessive} Longevity profile suggests " +
                $"a life expectancy of about {expectedAge} years.";
        }

        var difference =
            person.Age
            - expectedAge;

        if (Math.Abs(
                difference)
            <= 2)
        {
            return
                $"{pronouns.Possessive} Longevity profile suggested " +
                $"about {expectedAge} years of life. " +
                $"{pronouns.Subject} died at {person.Age}, very close " +
                "to that expectation.";
        }

        if (difference > 0)
        {
            return
                $"{pronouns.Possessive} Longevity profile suggested " +
                $"about {expectedAge} years of life. " +
                $"{pronouns.Subject} actually lived to {person.Age}, " +
                $"{difference} years longer than expected.";
        }

        return
            $"{pronouns.Possessive} Longevity profile suggested " +
            $"about {expectedAge} years of life. " +
            $"{pronouns.Subject} died at {person.Age}, " +
            $"{Math.Abs(difference)} years earlier than expected.";
    }

    private string FertilityDescription(
        IPerson person,
        int value,
        bool isAlive,
        Sex sex,
        Pronouns pronouns)
    {
        var children =
            _family.GetChildren(
                person)
            .Count;

        if (!isAlive
            && person.Age < AdultAge)
        {
            return
                $"{pronouns.Subject} died before adulthood, so " +
                $"{pronouns.PossessiveLower} fertility potential never " +
                "had a meaningful opportunity to translate into a family.";
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
            var adulthoodText =
                person.Age < AdultAge
                    ? "later in adulthood"
                    : "over the course of adult life";

            return
                $"{pronouns.Possessive} Fertility profile suggests " +
                $"{expectation.Description} {adulthoodText}.";
        }

        var comparison =
            CompareChildrenToExpectation(
                children,
                expectation);

        return
            $"{pronouns.Possessive} Fertility profile suggested " +
            $"{expectation.Description}. " +
            $"{pronouns.Subject} ultimately had " +
            $"{ChildCountText(children)}, {comparison}.";
    }

    private static string AppealDescription(
        IPerson person,
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (isAlive)
        {
            if (person.Age < AdultAge)
            {
                return
                    $"As a child, {pronouns.PossessiveLower} appearance " +
                    "is still developing; " +
                    ChildAppealPresent(
                        value,
                        pronouns);
            }

            if (person.Age >= ElderAge)
            {
                return
                    $"In later life, {pronouns.SubjectLower} " +
                    ElderAppealPresent(
                        value);
            }

            return
                $"In adulthood, {pronouns.SubjectLower} " +
                AdultAppealPresent(
                    value);
        }

        if (person.Age < AdultAge)
        {
            return
                $"As a child, {pronouns.SubjectLower} " +
                ChildAppealPast(
                    value);
        }

        if (person.Age >= ElderAge)
        {
            return
                $"In later life, {pronouns.SubjectLower} " +
                ElderAppealPast(
                    value);
        }

        return
            $"In adulthood, {pronouns.SubjectLower} " +
            AdultAppealPast(
                value);
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
                    "suggested that " +
                    $"{pronouns.SubjectLower} would have been relatively weak.",

                3 =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggested average future strength.",

                _ =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggested that " +
                    $"{pronouns.SubjectLower} would have grown unusually strong."
            };
        }

        if (isAlive)
        {
            return value switch
            {
                <= 2 =>
                    $"Physically, {pronouns.SubjectLower} is weak and " +
                    "may struggle with demanding manual work.",

                3 =>
                    $"Physically, {pronouns.SubjectLower} has average strength.",

                _ =>
                    $"Physically, {pronouns.SubjectLower} is notably strong " +
                    "and well suited to demanding physical work."
            };
        }

        return value switch
        {
            <= 2 =>
                $"Physically, {pronouns.SubjectLower} was weak and " +
                "was poorly suited to demanding manual work.",

            3 =>
                $"Physically, {pronouns.SubjectLower} had average strength.",

            _ =>
                $"Physically, {pronouns.SubjectLower} was notably strong " +
                "and well suited to demanding physical work."
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
                    $"Mentally, {pronouns.SubjectLower} has very limited " +
                    "intellectual ability.",

                2 =>
                    $"Mentally, {pronouns.SubjectLower} is somewhat " +
                    "below average in intellect.",

                3 =>
                    $"Mentally, {pronouns.SubjectLower} has an average intellect.",

                4 =>
                    $"Mentally, {pronouns.SubjectLower} is bright and " +
                    "quick to understand difficult matters.",

                _ =>
                    $"Mentally, {pronouns.SubjectLower} is exceptionally " +
                    "gifted, with a remarkably sharp intellect."
            };
        }

        return value switch
        {
            1 =>
                $"Mentally, {pronouns.SubjectLower} had very limited " +
                "intellectual ability.",

            2 =>
                $"Mentally, {pronouns.SubjectLower} was somewhat " +
                "below average in intellect.",

            3 =>
                $"Mentally, {pronouns.SubjectLower} had an average intellect.",

            4 =>
                $"Mentally, {pronouns.SubjectLower} was bright and " +
                "quick to understand difficult matters.",

            _ =>
                $"Mentally, {pronouns.SubjectLower} was exceptionally " +
                "gifted, with a remarkably sharp intellect."
        };
    }

    private static string ChildAppealPresent(
        int value,
        Pronouns pronouns)
    {
        return value switch
        {
            1 =>
                $"{pronouns.SubjectLower} currently has rather plain features.",

            2 =>
                $"{pronouns.SubjectLower} currently has an unremarkable appearance.",

            3 =>
                $"{pronouns.SubjectLower} currently has a pleasant, ordinary appearance.",

            4 =>
                $"{pronouns.SubjectLower} is already notably attractive for " +
                $"{pronouns.PossessiveLower} age.",

            _ =>
                $"{pronouns.SubjectLower} has unusually striking features for " +
                $"{pronouns.PossessiveLower} age."
        };
    }

    private static string ChildAppealPast(
        int value)
    {
        return value switch
        {
            1 =>
                "had rather plain features.",

            2 =>
                "had an unremarkable appearance.",

            3 =>
                "had a pleasant, ordinary appearance.",

            4 =>
                "was notably attractive for their age.",

            _ =>
                "had unusually striking features for their age."
        };
    }

    private static string AdultAppealPresent(
        int value)
    {
        return value switch
        {
            1 =>
                "is considered very unattractive.",

            2 =>
                "is not particularly visually appealing.",

            3 =>
                "has a pleasant and modest appearance.",

            4 =>
                "is regarded as good-looking.",

            _ =>
                "has a striking and highly attractive presence."
        };
    }

    private static string AdultAppealPast(
        int value)
    {
        return value switch
        {
            1 =>
                "was considered very unattractive.",

            2 =>
                "was not considered particularly visually appealing.",

            3 =>
                "had a pleasant and modest appearance.",

            4 =>
                "was regarded as good-looking.",

            _ =>
                "had a striking and highly attractive presence."
        };
    }

    private static string ElderAppealPresent(
        int value)
    {
        return value switch
        {
            <= 2 =>
                "has a fairly plain appearance and modest social presence.",

            3 =>
                "has an ordinary but pleasant appearance.",

            4 =>
                "retains a distinguished and attractive appearance.",

            _ =>
                "retains a remarkably striking and charismatic presence."
        };
    }

    private static string ElderAppealPast(
        int value)
    {
        return value switch
        {
            <= 2 =>
                "had a fairly plain appearance and modest social presence.",

            3 =>
                "had an ordinary but pleasant appearance.",

            4 =>
                "retained a distinguished and attractive appearance.",

            _ =>
                "retained a remarkably striking and charismatic presence."
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
            "broadly in line with the fertility profile";
    }

    private sealed record FertilityExpectation(
        string Description,
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
                        "little or no realistic prospect of children",
                        0,
                        0),

                1 =>
                    new FertilityExpectation(
                        "a very small family, usually no more than one child",
                        0,
                        1),

                2 =>
                    new FertilityExpectation(
                        "a relatively small family of roughly one or two children",
                        1,
                        2),

                3 =>
                    new FertilityExpectation(
                        "a moderate family of roughly two or three children",
                        2,
                        3),

                4 =>
                    new FertilityExpectation(
                        "a large family of roughly three or four children",
                        3,
                        4),

                _ =>
                    new FertilityExpectation(
                        "a very large family of four or more children",
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
