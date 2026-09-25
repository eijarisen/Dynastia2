using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

[PersistedComponentId("thoughts.person")]
public sealed class PersonThoughtComponent
{
    public int Year { get; set; }

    public string ThoughtId { get; set; } =
        string.Empty;

    public string Topic { get; set; } =
        string.Empty;

    public string Text { get; set; } =
        string.Empty;

    public string MoodId { get; set; } =
        string.Empty;

    public string MoodEmoji { get; set; } =
        string.Empty;

    public string TopicEmoji { get; set; } =
        string.Empty;

    // Retained so older saves deserialize safely. Newly generated thoughts
    // mirror MoodEmoji here until the compatibility field can be removed.
    [Obsolete("Legacy save compatibility. Use MoodEmoji.")]
    public string Emoji { get; set; } =
        string.Empty;

    public int Salience { get; set; }

    public string? SourceId { get; set; }
}
