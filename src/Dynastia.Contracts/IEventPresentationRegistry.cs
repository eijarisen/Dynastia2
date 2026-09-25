namespace Dynastia.Contracts;

public interface IEventPresentationRegistry
{
    void Register(
        string eventType,
        EventPresentationMetadata presentation,
        string ownerId = "unspecified");

    void RegisterPrefix(
        string prefix,
        EventPresentationMetadata presentation,
        string ownerId = "unspecified");

    EventPresentationMetadata Resolve(string eventType);
}
