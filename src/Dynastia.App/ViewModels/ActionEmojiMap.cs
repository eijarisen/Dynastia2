using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public static class ActionEmojiMap
{
    public static string GetEmoji(GameActionDefinition definition) =>
        definition.Presentation?.Emoji is string emoji
        && !string.IsNullOrWhiteSpace(emoji)
            ? emoji
            : GetEmoji(definition.Id);

    public static string Format(GameActionDefinition definition, string label) =>
        $"{GetEmoji(definition)} {label}";

    // Compatibility fallback for legacy/third-party definitions that do not
    // provide ActionPresentationMetadata. First-party actions own their emoji.
    public static string GetEmoji(string actionId) =>
        actionId.StartsWith("career.", StringComparison.OrdinalIgnoreCase)
            ? "💼"
            : actionId.StartsWith("relationship.", StringComparison.OrdinalIgnoreCase)
                ? "💞"
                : actionId.StartsWith("household.", StringComparison.OrdinalIgnoreCase)
                    ? "🏠"
                    : actionId.StartsWith("family_relations.", StringComparison.OrdinalIgnoreCase)
                        ? "👪"
                        : actionId.StartsWith("loan.", StringComparison.OrdinalIgnoreCase)
                            ? "🏦"
                            : actionId.StartsWith("craft.", StringComparison.OrdinalIgnoreCase)
                                ? "🛠️"
                                : actionId.StartsWith("farming.", StringComparison.OrdinalIgnoreCase)
                                    ? "🌾"
                                    : actionId.StartsWith("education.", StringComparison.OrdinalIgnoreCase)
                                        ? "🎓"
                                        : actionId.StartsWith("church.", StringComparison.OrdinalIgnoreCase)
                                            ? "⛪"
                                            : actionId.StartsWith("community.", StringComparison.OrdinalIgnoreCase)
                                                ? "🏛️"
                                                : "⚙️";

    public static string Format(
        string actionId,
        string label) =>
        $"{GetEmoji(actionId)} {label}";
}
