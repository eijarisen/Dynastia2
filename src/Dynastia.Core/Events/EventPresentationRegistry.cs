using Dynastia.Contracts;

namespace Dynastia.Core.Events;

public sealed class EventPresentationRegistry : IEventPresentationRegistry
{
    private sealed record Registration(
        string Key,
        EventPresentationMetadata Presentation,
        string OwnerId,
        long Order);

    private readonly Dictionary<string, Registration> _exact =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Registration> _prefixes =
        new(StringComparer.OrdinalIgnoreCase);

    private long _nextOrder;

    public void Register(
        string eventType,
        EventPresentationMetadata presentation,
        string ownerId = "unspecified")
    {
        RegisterCore(
            _exact,
            eventType,
            presentation,
            ownerId,
            "event type");
    }

    public void RegisterPrefix(
        string prefix,
        EventPresentationMetadata presentation,
        string ownerId = "unspecified")
    {
        RegisterCore(
            _prefixes,
            prefix,
            presentation,
            ownerId,
            "event prefix");
    }

    public EventPresentationMetadata Resolve(string eventType)
    {
        if (!string.IsNullOrWhiteSpace(eventType)
            && _exact.TryGetValue(eventType, out var exact))
        {
            return exact.Presentation;
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var prefix = _prefixes.Values
                .Where(candidate =>
                    eventType.StartsWith(
                        candidate.Key,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(candidate => candidate.Key.Length)
                .ThenBy(candidate => candidate.Order)
                .FirstOrDefault();

            if (prefix is not null)
                return prefix.Presentation;
        }

        return new EventPresentationMetadata();
    }

    private void RegisterCore(
        IDictionary<string, Registration> registrations,
        string key,
        EventPresentationMetadata presentation,
        string ownerId,
        string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        if (registrations.TryGetValue(key, out var existing))
        {
            if (existing.OwnerId.Equals(ownerId, StringComparison.OrdinalIgnoreCase)
                && existing.Presentation == presentation)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Event presentation {kind} '{key}' is already owned by '{existing.OwnerId}' and cannot also be registered by '{ownerId}'.");
        }

        registrations[key] = new Registration(
            key,
            presentation,
            ownerId,
            _nextOrder++);
    }
}
