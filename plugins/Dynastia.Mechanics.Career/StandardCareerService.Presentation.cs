using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class StandardCareerService
{
    public string GetCareerEmoji(
        string? careerId)
    {
        if (string.IsNullOrWhiteSpace(
            careerId))
        {
            return CareerPresentationDefaults.DefaultCareerEmoji;
        }

        var emoji =
            _catalog.Find(careerId)?.Emoji;

        return string.IsNullOrWhiteSpace(emoji)
            ? CareerPresentationDefaults.DefaultCareerEmoji
            : emoji;
    }

    public string GetOccupationEmoji(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        // Mirror the existing job-title status precedence. Presentation
        // must never initialize career state or consume simulation RNG.
        if (person.Tags.Has(
            "role.nanny"))
        {
            return "🧑‍🍼";
        }

        if (person.Tags.Has(
            "state.imprisoned"))
        {
            return "⛓️";
        }

        var career =
            person.Components.Get<CareerComponent>();

        if (person.Tags.Has(
            "role.family_nanny"))
        {
            return "🧑‍🍼";
        }

        var activeCraft =
            HasCraftOccupation(person)
                ? _craftResolver()?.GetActiveCraft(person)
                : null;

        if (activeCraft is not null)
            return activeCraft.Emoji;

        if (career?.IsRetired == true)
            return "🕰️";

        var jobLevel =
            career?.JobLevel
            ?? 0;

        if (_family.GetSex(person) == Sex.Female
            && jobLevel == 0
            && _family.GetChildren(person).Count > 0)
        {
            return "🏠";
        }

        if (person.Age < 18)
            return "📖";

        if (jobLevel <= 0)
            return "🔎";

        return GetCareerEmoji(
            career?.CareerId);
    }
}
