using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal static class ThoughtMoodCatalog
{
    private static readonly IReadOnlyDictionary<string, string> Emojis =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ThoughtMoodIds.Neutral] = string.Empty,
            [ThoughtMoodIds.Happy] = "😄",
            [ThoughtMoodIds.Pleased] = "😊",
            [ThoughtMoodIds.Relieved] = "😌",
            [ThoughtMoodIds.Concerned] = "😟",
            [ThoughtMoodIds.Sad] = "😔",
            [ThoughtMoodIds.Grieving] = "😭",
            [ThoughtMoodIds.Angry] = "😠",
            [ThoughtMoodIds.Afraid] = "😨",
            [ThoughtMoodIds.Sick] = "🤒",
            [ThoughtMoodIds.Distressed] = "😣",
            [ThoughtMoodIds.Exhausted] = "😩"
        };

    public static bool IsKnown(string? moodId) =>
        !string.IsNullOrWhiteSpace(moodId)
        && Emojis.ContainsKey(moodId);

    public static string Normalize(string? moodId) =>
        IsKnown(moodId)
            ? moodId!.ToLowerInvariant()
            : ThoughtMoodIds.Neutral;

    public static string ResolveEmoji(string? moodId) =>
        Emojis.TryGetValue(
            moodId ?? string.Empty,
            out var emoji)
                ? emoji
                : string.Empty;
}
