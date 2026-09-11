namespace Dynastia.Mechanics.Thoughts;

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
