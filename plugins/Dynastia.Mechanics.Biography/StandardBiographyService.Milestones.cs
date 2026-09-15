using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed partial class StandardBiographyService
{
    private void EnsureGeneratedAdultLifeMilestones(
        IPerson person)
    {
        if (_family.IsBloodline(person)
            || person.BirthDate
                is not GameDate birthDate)
        {
            return;
        }

        var background =
            _family.GetGeneratedFamilyBackground(
                person);

        if (background is null)
            return;

        AddEntryIfMissing(
            person,
            birthDate.Year,
            $"👶 Was born to {background.FatherName} and " +
            $"{background.MotherName}.");

        var adulthoodYear =
            birthDate.Year + 18;

        var lifeEndYear =
            person.DeathDate?.Year
            ?? _gameState.Year;

        if (adulthoodYear <= lifeEndYear)
        {
            AddEntryIfMissing(
                person,
                adulthoodYear,
                "🧑 Became an adult.");
        }
    }

    private static bool IsMinorRelativeIllnessEntry(
        BiographyEntry entry)
    {
        var message = entry.Message;

        if (!message.StartsWith(
                "🤧 ",
                StringComparison.Ordinal))
        {
            return false;
        }

        var relativePrefix =
            message.StartsWith(
                "🤧 His ",
                StringComparison.Ordinal)
            || message.StartsWith(
                "🤧 Her ",
                StringComparison.Ordinal);

        return relativePrefix
            && message.Contains(
                "fell ill with",
                StringComparison.OrdinalIgnoreCase);
    }
}
