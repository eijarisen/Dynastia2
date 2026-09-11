namespace Dynastia.Contracts;

public sealed record ThoughtCandidate(
    string Id,
    string Topic,
    string DeduplicationKey,
    int Salience,
    string Emoji,
    string SourceKind,
    string? SourceId,
    string WordingKey,
    IReadOnlyDictionary<string, string> Context)
{
    public ThoughtCandidate(
        string id,
        string topic,
        string deduplicationKey,
        int salience,
        string emoji,
        string sourceKind,
        string? sourceId,
        string wordingKey)
        : this(
            id,
            topic,
            deduplicationKey,
            salience,
            emoji,
            sourceKind,
            sourceId,
            wordingKey,
            new Dictionary<string, string>())
    {
    }
}
