using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class StandardCareerService
{
    private const string DefaultCareerEmoji = "💼";

    public string GetCareerEmoji(
        string? careerId)
    {
        if (string.IsNullOrWhiteSpace(
            careerId))
        {
            return DefaultCareerEmoji;
        }

        var emoji =
            _catalog.Find(careerId)?.Emoji;

        return string.IsNullOrWhiteSpace(emoji)
            ? DefaultCareerEmoji
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

        if (career?.IsRetired == true)
            return "🕰️";

        if (person.Tags.Has(
            "role.family_nanny"))
        {
            return "🧑‍🍼";
        }

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
