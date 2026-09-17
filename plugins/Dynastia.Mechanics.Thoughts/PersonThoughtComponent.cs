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

    public string Emoji { get; set; } =
        string.Empty;

    public int Salience { get; set; }

    public string? SourceId { get; set; }
}
