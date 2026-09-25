using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed partial class StandardBiographyService
{
    private static bool IsFamilyMoneyEvent(
        string type) =>
        type.Equals(
            "family_relations.money_received",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "family_relations.money_given",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "family_relations.money_refused",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "farmland.given",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "farmland.received",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "farmland.request_refused",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsImportantForRelatives(
        string type)
    {
        return type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.married",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.partnered",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.remarried",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.low_satisfaction_divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.prison_divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.affair",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "justice.crime",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "health.serious_illness",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "birth.condition",
                StringComparison.OrdinalIgnoreCase);
    }

    private string FormatWithEmoji(
        string type,
        string message)
    {
        var prefix = GetEmojiPrefix(type);
        return $"{prefix}{message}";
    }

    private string GetEmojiPrefix(string type)
    {
        var emoji = _eventPresentation.Resolve(type).Emoji;
        return string.IsNullOrWhiteSpace(emoji)
            ? string.Empty
            : $"{emoji} ";
    }

    private static string? GetEventText(
        GameEvent gameEvent)
    {
        return gameEvent.Data.TryGetValue(
            "text",
            out var text)
            ? text
            : null;
    }

    private static int GetInt(
        GameEvent gameEvent,
        string key,
        int fallback)
    {
        return gameEvent.Data.TryGetValue(
                key,
                out var text)
            && int.TryParse(
                text,
                out var value)
                    ? value
                    : fallback;
    }

    private static string Possessive(
        IPerson person)
    {
        return person.Tags.Has(
            "sex.male")
                ? "His"
                : "Her";
    }

    private static bool IsAlive(
        IPerson? person)
    {
        return person is not null
            && person.Tags.Has(
                "state.alive");
    }

    private static string ToOrdinalWord(
        int value)
    {
        return value switch
        {
            1 => "first",
            2 => "second",
            3 => "third",
            _ => $"{value}th"
        };
    }

}
