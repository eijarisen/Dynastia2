namespace Dynastia.Contracts;

public sealed record ThoughtCandidate(
    string Id,
    string Topic,
    string DeduplicationKey,
    int Salience,
    string MoodId,
    string TopicEmoji,
    ThoughtSalienceTraits SalienceTraits,
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
        string moodId,
        string topicEmoji,
        ThoughtSalienceTraits salienceTraits,
        string sourceKind,
        string? sourceId,
        string wordingKey)
        : this(
            id,
            topic,
            deduplicationKey,
            salience,
            moodId,
            topicEmoji,
            salienceTraits,
            sourceKind,
            sourceId,
            wordingKey,
            new Dictionary<string, string>())
    {
    }
}
