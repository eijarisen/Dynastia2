using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed partial class AboutTextBuilder
{
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

}
