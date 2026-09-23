using System.Reflection;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.TestSupport;

/// <summary>
/// Canonical, comparison-only serialization of authoritative simulation state.
/// People, events and queued actions retain their gameplay order; tags,
/// persisted components and dictionary entries are sorted only to make
/// logically identical snapshots byte-comparable.
/// </summary>
public static class CanonicalSimulationSnapshot
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase
        };

    public static string Capture(
        IGameState gameState,
        IGameEventBus events,
        IReadOnlyList<QueuedActionInfo> queuedActions,
        IStatefulGameRandom random,
        Guid? selectedPersonId = null,
        Guid? activeControllerId = null,
        IBiographyService? biography = null)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(queuedActions);
        ArgumentNullException.ThrowIfNull(random);

        var snapshot =
            new Snapshot(
                gameState.DynastySurname,
                gameState.Year,
                gameState.StartYear,
                random.CaptureState(),
                selectedPersonId,
                activeControllerId,
                gameState.People
                    .Select(CapturePerson)
                    .ToList(),
                events.AllEvents
                    .Select(CaptureEvent)
                    .ToList(),
                queuedActions
                    .Select(CaptureQueuedAction)
                    .ToList(),
                CaptureBiography(
                    biography));

        return JsonSerializer.Serialize(
            snapshot,
            JsonOptions);
    }

    private static PersonSnapshot CapturePerson(
        IPerson person)
    {
        var components =
            person.Components.All
                .Select(CaptureComponent)
                .OrderBy(
                    component => component.Id,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    component => component.Id,
                    StringComparer.Ordinal)
                .ToList();

        return new PersonSnapshot(
            person.Id,
            person.Name,
            person.Surname,
            person.MaidenName,
            person.Age,
            person.BirthDate,
            person.DeathDate,
            person.Tags.All
                .OrderBy(
                    tag => tag,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    tag => tag,
                    StringComparer.Ordinal)
                .ToList(),
            components);
    }

    private static ComponentSnapshot CaptureComponent(
        object component)
    {
        var type = component.GetType();
        var id =
            type.GetCustomAttribute<PersistedComponentIdAttribute>()
                ?.Id
            ?? throw new InvalidOperationException(
                $"Persisted component '{type.FullName}' has no stable component ID.");

        var json =
            JsonSerializer.Serialize(
                component,
                type,
                JsonOptions);

        using var document =
            JsonDocument.Parse(json);

        return new ComponentSnapshot(
            id,
            Canonicalize(
                document.RootElement));
    }

    private static EventSnapshot CaptureEvent(
        GameEvent gameEvent) =>
        new(
            gameEvent.Type,
            gameEvent.Year,
            gameEvent.SubjectId,
            gameEvent.RelatedPersonIds.ToList(),
            gameEvent.Data
                .OrderBy(
                    pair => pair.Key,
                    StringComparer.Ordinal)
                .Select(
                    pair =>
                        new KeyValueSnapshot(
                            pair.Key,
                            pair.Value))
                .ToList());

    private static IReadOnlyList<BiographySnapshot> CaptureBiography(
        IBiographyService? biography)
    {
        if (biography is null)
            return [];

        return biography.ExportBiographyState()
            .OrderBy(
                pair => pair.Key)
            .Select(
                pair =>
                    new BiographySnapshot(
                        pair.Key,
                        pair.Value.ToList()))
            .ToList();
    }

    private static QueuedActionSnapshot CaptureQueuedAction(
        QueuedActionInfo action) =>
        new(
            action.ActionId,
            action.Label,
            action.Phase,
            action.ActorId,
            action.TargetId,
            action.Description,
            action.Origin,
            action.ActorHouseholdId,
            action.Parameters?
                .OrderBy(
                    pair => pair.Key,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    pair => pair.Key,
                    StringComparer.Ordinal)
                .Select(
                    pair =>
                        new KeyValueSnapshot(
                            pair.Key,
                            pair.Value))
                .ToList()
            ?? []);

    private static string Canonicalize(
        JsonElement element)
    {
        using var stream =
            new MemoryStream();

        using (var writer =
            new Utf8JsonWriter(stream))
        {
            WriteCanonical(
                writer,
                element);
        }

        return System.Text.Encoding.UTF8.GetString(
            stream.ToArray());
    }

    private static void WriteCanonical(
        Utf8JsonWriter writer,
        JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (var property in
                    element.EnumerateObject()
                        .OrderBy(
                            property => property.Name,
                            StringComparer.Ordinal))
                {
                    writer.WritePropertyName(
                        property.Name);

                    WriteCanonical(
                        writer,
                        property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();

                foreach (var item in
                    element.EnumerateArray())
                {
                    WriteCanonical(
                        writer,
                        item);
                }

                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private sealed record Snapshot(
        string DynastySurname,
        int Year,
        int StartYear,
        GameRandomState RandomState,
        Guid? SelectedPersonId,
        Guid? ActiveControllerId,
        IReadOnlyList<PersonSnapshot> People,
        IReadOnlyList<EventSnapshot> Events,
        IReadOnlyList<QueuedActionSnapshot> QueuedActions,
        IReadOnlyList<BiographySnapshot> Biography);

    private sealed record PersonSnapshot(
        Guid Id,
        string Name,
        string Surname,
        string? MaidenName,
        int Age,
        GameDate? BirthDate,
        GameDate? DeathDate,
        IReadOnlyList<string> Tags,
        IReadOnlyList<ComponentSnapshot> Components);

    private sealed record ComponentSnapshot(
        string Id,
        string Json);

    private sealed record EventSnapshot(
        string Type,
        int Year,
        Guid? SubjectId,
        IReadOnlyList<Guid> RelatedPersonIds,
        IReadOnlyList<KeyValueSnapshot> Data);

    private sealed record QueuedActionSnapshot(
        string ActionId,
        string Label,
        YearPhase Phase,
        Guid ActorId,
        Guid TargetId,
        string? Description,
        ActionExecutionOrigin Origin,
        Guid? ActorHouseholdId,
        IReadOnlyList<KeyValueSnapshot> Parameters);

    private sealed record BiographySnapshot(
        Guid PersonId,
        IReadOnlyList<BiographyEntry> Entries);

    private sealed record KeyValueSnapshot(
        string Key,
        string Value);
}
