namespace Dynastia.Contracts;

public sealed record GameEvent
{
    public required string Type { get; init; }
    public required int Year { get; init; }

    public Guid? SubjectId { get; init; }

    public IReadOnlyList<Guid> RelatedPersonIds { get; init; } =
        Array.Empty<Guid>();

    public IReadOnlyDictionary<string, string> Data { get; init; } =
        new Dictionary<string, string>();
}
