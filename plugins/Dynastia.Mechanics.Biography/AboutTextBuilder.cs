using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed class AboutTextBuilder
{
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
                    _family.GetFather(person));

            motherName =
                NameOrUnknown(
                    _family.GetMother(person));
        }

        var birthOrder =
            GetBirthOrderText(
                person);

        var stats =
            _stats.GetStats(person)
                .ToDictionary(
                    stat => stat.Id,
                    stat => stat.Value,
                    StringComparer.OrdinalIgnoreCase);

        var pronoun =
            _family.GetSex(person) == Sex.Male
                ? "He"
                : "She";

        var possessive =
            _family.GetSex(person) == Sex.Male
                ? "His"
                : "Her";

        var birthDate =
            person.BirthDate?.ToString()
            ?? "an unknown date";

        return
            $"{_family.GetDisplayName(person)} was born on {birthDate} " +
            $"to {fatherName} and {motherName}{birthOrder}. " +
            $"{pronoun} was born with {ImmunityDescription(stats["immunity"])} " +
            $"{possessive} family history suggests {pronoun.ToLowerInvariant()} " +
            $"{LongevityDescription(stats["longevity"])} " +
            $"{pronoun} {FertilityDescription(stats["fertility"])} " +
            $"In appearance, {pronoun.ToLowerInvariant()} " +
            $"{AppealDescription(stats["appeal"])} " +
            $"Physically, {pronoun.ToLowerInvariant()} " +
            $"{StrengthDescription(stats["strength"])} " +
            $"Mentally, {pronoun.ToLowerInvariant()} " +
            $"{IntellectDescription(stats["intellect"])}";
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
            _family.GetChildren(father)
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
                            .IndexOf(child))
                .ToList();

        var index =
            siblings.FindIndex(
                sibling =>
                    sibling.Id
                    == person.Id);

        return index < 0
            ? string.Empty
            : $" as their {ToOrdinal(index + 1)} child";
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
        int value)
    {
        if (value <= 2)
        {
            return
                "a delicate constitution, making them prone to sickness.";
        }

        if (value >= 4)
        {
            return
                "a robust constitution, granting a strong resistance to illness.";
        }

        return
            "a generally healthy nature.";
    }

    private static string LongevityDescription(
        int value)
    {
        if (value <= 2)
        {
            return
                "hails from a line not known for longevity.";
        }

        if (value >= 4)
        {
            return
                "descends from a long-lived family and may be blessed with a great many years.";
        }

        return
            "is expected to live an average lifespan.";
    }

    private static string AppealDescription(
        int value)
    {
        return value switch
        {
            1 =>
                "is considered as ugly as a creature from a fairytale.",

            2 =>
                "is not particularly visually appealing.",

            3 =>
                "has a decent and modest appearance.",

            4 =>
                "is regarded as good-looking.",

            _ =>
                "is blessed with a very attractive and compelling presence."
        };
    }

    private static string FertilityDescription(
        int value)
    {
        if (value == 0)
        {
            return
                "is unfortunately infertile and will be unable to produce heirs.";
        }

        if (value <= 2)
        {
            return
                "may face challenges in bearing children.";
        }

        if (value == 3)
        {
            return
                "is expected to have little trouble raising a family.";
        }

        return
            "comes from a large and fertile family, suggesting an easy time having children.";
    }

    private static string StrengthDescription(
        int value)
    {
        if (value <= 2)
        {
            return
                "is weak, which could present challenges in finding manual labor.";
        }

        if (value == 3)
        {
            return
                "possesses average physical strength.";
        }

        return
            "is gifted with great strength, a respectable trait that will make finding work easier.";
    }

    private static string IntellectDescription(
        int value)
    {
        return value switch
        {
            1 =>
                "is, to put it mildly, as dumb as a rock.",

            2 =>
                "is not known for being particularly bright.",

            3 =>
                "has an average intellect.",

            4 =>
                "is known for being quite smart and prominent.",

            _ =>
                "is a veritable genius, possessing a sharp mind destined for success."
        };
    }
}
