namespace Dynastia.Contracts;

public sealed record PersonThoughtSnapshot(
    int Year,
    string ThoughtId,
    string Topic,
    string Text,
    string MoodId,
    string MoodEmoji,
    string TopicEmoji,
    int Salience,
    string? SourceId)
{
    // Compatibility alias for callers that still render the old single field.
    public string Emoji => MoodEmoji;
}
