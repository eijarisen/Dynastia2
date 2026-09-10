namespace Dynastia.Mechanics.Justice;

public sealed class CrimeDefinition
{
    public string Id { get; init; } =
        string.Empty;

    public string Name { get; init; } =
        string.Empty;

    public int SentenceMin { get; init; }

    public int SentenceMax { get; init; }

    public string Description { get; init; } =
        string.Empty;

    public double Weight { get; init; }
}
