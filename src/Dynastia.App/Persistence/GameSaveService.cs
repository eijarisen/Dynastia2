using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dynastia.Contracts;

namespace Dynastia.App.Persistence;

public sealed class GameSaveService
{
    private const int CurrentFormatVersion =
        1;

    private const string CipherKey =
        "dynastia_secret_key";

    private readonly IGameState _gameState;
    private readonly ISelectionService _selection;
    private readonly ISuccessionService _succession;
    private readonly IGameEventBus _events;
    private readonly IActionRegistry _actions;
    private readonly IBiographyService? _biography;

    private static readonly JsonSerializerOptions
        SaveJsonOptions =
            new()
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,

                PropertyNameCaseInsensitive =
                    true
            };

    private static readonly JsonSerializerOptions
        ComponentJsonOptions =
            new()
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,

                PropertyNameCaseInsensitive =
                    true,

                PreferredObjectCreationHandling =
                    JsonObjectCreationHandling.Populate
            };

    public GameSaveService(
        IGameState gameState,
        ISelectionService selection,
        ISuccessionService succession,
        IGameEventBus events,
        IActionRegistry actions,
        IBiographyService? biography)
    {
        _gameState = gameState;
        _selection = selection;
        _succession = succession;
        _events = events;
        _actions = actions;
        _biography = biography;
    }

    public string GetSuggestedFileName()
    {
        var surname =
            SanitizeFileNamePart(
                _gameState.DynastySurname);

        return
            $"dynastia_save_{surname}_{_gameState.Year}.txt";
    }

    public void Save(
        Stream stream,
        GameUiSaveState uiState)
    {
        ArgumentNullException.ThrowIfNull(
            stream);

        if (_gameState.People.Count == 0)
        {
            throw new InvalidOperationException(
                "There is no active dynasty to save.");
        }

        var envelope =
            Capture(
                uiState);

        var json =
            JsonSerializer.Serialize(
                envelope,
                SaveJsonOptions);

        var encrypted =
            Encrypt(
                json);

        using var writer =
            new StreamWriter(
                stream,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                        false),
                bufferSize: 1024,
                leaveOpen: true);

        writer.Write(
            encrypted);

        writer.Flush();
    }

    public GameUiSaveState Load(
        Stream stream,
        GameUiSaveState currentUiState)
    {
        ArgumentNullException.ThrowIfNull(
            stream);

        string encrypted;

        using (var reader =
            new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks:
                    true,
                bufferSize: 1024,
                leaveOpen: true))
        {
            encrypted =
                reader.ReadToEnd();
        }

        if (string.IsNullOrWhiteSpace(
            encrypted))
        {
            throw new InvalidDataException(
                "The selected save file is empty.");
        }

        DesktopSaveEnvelope loaded;

        try
        {
            var json =
                Decrypt(
                    encrypted.Trim());

            DetectLegacyBrowserSave(
                json);

            loaded =
                JsonSerializer.Deserialize<
                    DesktopSaveEnvelope>(
                        json,
                        SaveJsonOptions)
                ?? throw new InvalidDataException(
                    "The save file contains no game state.");
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                "The selected file is not a valid Dynastia save file.",
                exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The save file is corrupted or has an invalid structure.",
                exception);
        }

        ValidateEnvelope(
            loaded);

        var prepared =
            PrepareComponents(
                loaded);

        DesktopSaveEnvelope? backup =
            null;

        PreparedComponents? preparedBackup =
            null;

        if (_gameState.People.Count > 0)
        {
            backup =
                Capture(
                    currentUiState);

            preparedBackup =
                PrepareComponents(
                    backup);
        }

        try
        {
            Apply(
                loaded,
                prepared);
        }
        catch
        {
            if (backup is not null
                && preparedBackup is not null)
            {
                try
                {
                    Apply(
                        backup,
                        preparedBackup);
                }
                catch
                {
                    // Preserve the original load exception.
                    // Rollback is best-effort only.
                }
            }

            throw;
        }

        return new GameUiSaveState(
            loaded.SelectedPersonId,
            loaded.ActiveControllerId,
            loaded.AlbumYear,
            loaded.IsLivingFamilyView,
            loaded.DetailsTabIndex);
    }

    private DesktopSaveEnvelope Capture(
        GameUiSaveState uiState)
    {
        var people =
            _gameState.People
                .Select(
                    CapturePerson)
                .ToList();

        var events =
            _events.AllEvents
                .Select(
                    GameEventSaveData.FromEvent)
                .ToList();

        var queued =
            _actions.GetAllQueuedActions()
                .Select(
                    QueuedActionSaveData.FromAction)
                .ToList();

        var biography =
            _biography?
                .ExportBiographyState()
                .Select(
                    pair =>
                        new BiographyPersonSaveData
                        {
                            PersonId =
                                pair.Key,

                            Entries =
                                pair.Value.ToList()
                        })
                .ToList()
            ?? [];

        return new DesktopSaveEnvelope
        {
            IsDynastiaSave =
                true,

            FormatVersion =
                CurrentFormatVersion,

            SaveKind =
                "DynastiaDesktop",

            DynastySurname =
                _gameState.DynastySurname,

            Year =
                _gameState.Year,

            SelectedPersonId =
                uiState.SelectedPersonId,

            ActiveControllerId =
                uiState.ActiveControllerId,

            AlbumYear =
                uiState.AlbumYear,

            IsLivingFamilyView =
                uiState.IsLivingFamilyView,

            DetailsTabIndex =
                uiState.DetailsTabIndex,

            People =
                people,

            Events =
                events,

            QueuedActions =
                queued,

            Biography =
                biography
        };
    }

    private static PersonSaveData CapturePerson(
        IPerson person)
    {
        var components =
            new List<ComponentSaveData>();

        foreach (var component in
            person.Components.All)
        {
            var type =
                component.GetType();

            var typeName =
                type.FullName
                ?? throw new InvalidOperationException(
                    "Cannot save a component with no type name.");

            var assemblyName =
                type.Assembly
                    .GetName()
                    .Name
                ?? throw new InvalidOperationException(
                    $"Cannot save component '{typeName}' " +
                    "because its assembly has no name.");

            string json;

            try
            {
                json =
                    JsonSerializer.Serialize(
                        component,
                        type,
                        ComponentJsonOptions);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Failed to serialize component " +
                    $"'{typeName}'.",
                    exception);
            }

            components.Add(
                new ComponentSaveData
                {
                    AssemblyName =
                        assemblyName,

                    TypeName =
                        typeName,

                    Json =
                        json
                });
        }

        return new PersonSaveData
        {
            Id =
                person.Id,

            Name =
                person.Name,

            Surname =
                person.Surname,

            MaidenName =
                person.MaidenName,

            Age =
                person.Age,

            BirthDate =
                person.BirthDate,

            DeathDate =
                person.DeathDate,

            Tags =
                person.Tags.All
                    .OrderBy(
                        tag =>
                            tag,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList(),

            Components =
                components
        };
    }

    private PreparedComponents PrepareComponents(
        DesktopSaveEnvelope envelope)
    {
        var resolvedAssemblies =
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(
                    assembly =>
                        !assembly.IsDynamic)
                .GroupBy(
                    assembly =>
                        assembly.GetName().Name
                        ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First(),
                    StringComparer.OrdinalIgnoreCase);

        var byPerson =
            new Dictionary<
                Guid,
                IReadOnlyList<PreparedComponent>>();

        foreach (var person in
            envelope.People)
        {
            var components =
                new List<PreparedComponent>();

            foreach (var saved in
                person.Components)
            {
                if (!resolvedAssemblies.TryGetValue(
                    saved.AssemblyName,
                    out var assembly))
                {
                    throw new InvalidDataException(
                        $"Save file requires component assembly " +
                        $"'{saved.AssemblyName}', but it is not loaded. " +
                        "The required plugin may be missing.");
                }

                var type =
                    assembly.GetType(
                        saved.TypeName,
                        throwOnError: false,
                        ignoreCase: false)
                    ?? throw new InvalidDataException(
                        $"Save file requires component type " +
                        $"'{saved.TypeName}', but it is unavailable.");

                object component;

                try
                {
                    component =
                        JsonSerializer.Deserialize(
                            saved.Json,
                            type,
                            ComponentJsonOptions)
                        ?? throw new InvalidDataException(
                            $"Component '{saved.TypeName}' " +
                            "contains no state.");
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException(
                        $"Component '{saved.TypeName}' " +
                        "is corrupted or incompatible.",
                        exception);
                }

                components.Add(
                    new PreparedComponent(
                        type,
                        component));
            }

            byPerson[
                person.Id] =
                    components;
        }

        return new PreparedComponents(
            byPerson);
    }

    private void Apply(
        DesktopSaveEnvelope envelope,
        PreparedComponents prepared)
    {
        _actions.RestoreQueuedActions(
            []);

        _events.RestoreEvents(
            []);

        _gameState.ClearPeople();

        _gameState.DynastySurname =
            envelope.DynastySurname;

        _gameState.Year =
            envelope.Year;

        foreach (var savedPerson in
            envelope.People)
        {
            var person =
                _gameState.CreatePerson(
                    savedPerson.Name,
                    savedPerson.Surname,
                    savedPerson.Age,
                    savedPerson.Id);

            person.MaidenName =
                savedPerson.MaidenName;

            person.BirthDate =
                savedPerson.BirthDate;

            person.DeathDate =
                savedPerson.DeathDate;

            foreach (var tag in
                savedPerson.Tags)
            {
                person.Tags.Add(
                    tag);
            }

            if (prepared.ByPerson.TryGetValue(
                person.Id,
                out var components))
            {
                foreach (var component in
                    components)
                {
                    person.Components.Set(
                        component.Type,
                        component.Value);
                }
            }
        }

        var restoredEvents =
            envelope.Events
                .Select(
                    saved =>
                        saved.ToEvent())
                .ToList();

        _events.RestoreEvents(
            restoredEvents);

        if (_biography is not null)
        {
            var biography =
                envelope.Biography
                    .ToDictionary(
                        saved =>
                            saved.PersonId,
                        saved =>
                            (IReadOnlyList<BiographyEntry>)
                                saved.Entries.ToList());

            _biography.RestoreBiographyState(
                biography);
        }

        var queue =
            envelope.QueuedActions
                .Select(
                    saved =>
                        saved.ToAction())
                .ToList();

        _actions.RestoreQueuedActions(
            queue);

        _selection.SelectedPersonId =
            envelope.SelectedPersonId;

        _succession.Refresh();

        if (envelope.ActiveControllerId
            is Guid activeId)
        {
            var active =
                _gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id
                            == activeId);

            if (active is not null
                && _succession.IsControllable(
                    active))
            {
                _succession.SetActiveController(
                    active);
            }
        }

        // A selected person may legally be spouse/child/deceased.
        // Restore it after controller selection, because switching
        // controller also selects that controller.
        if (envelope.SelectedPersonId
            is Guid selectedId
            && _gameState.People.Any(
                person =>
                    person.Id == selectedId))
        {
            _selection.SelectedPersonId =
                selectedId;
        }
    }

    private static void ValidateEnvelope(
        DesktopSaveEnvelope envelope)
    {
        if (!envelope.IsDynastiaSave)
        {
            throw new InvalidDataException(
                "This is not a Dynastia save file.");
        }

        if (!string.Equals(
                envelope.SaveKind,
                "DynastiaDesktop",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "This Dynastia save belongs to an unsupported edition.");
        }

        if (envelope.FormatVersion <= 0
            || envelope.FormatVersion
                > CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Unsupported save format version " +
                $"{envelope.FormatVersion}.");
        }

        if (string.IsNullOrWhiteSpace(
            envelope.DynastySurname))
        {
            throw new InvalidDataException(
                "The save file has no dynasty surname.");
        }

        if (envelope.Year < 1)
        {
            throw new InvalidDataException(
                "The save file contains an invalid year.");
        }

        if (envelope.People.Count == 0)
        {
            throw new InvalidDataException(
                "The save file contains no people.");
        }

        var ids =
            new HashSet<Guid>();

        foreach (var person in
            envelope.People)
        {
            if (person.Id == Guid.Empty
                || !ids.Add(person.Id))
            {
                throw new InvalidDataException(
                    "The save file contains duplicate or invalid person IDs.");
            }

            if (string.IsNullOrWhiteSpace(
                    person.Name)
                || string.IsNullOrWhiteSpace(
                    person.Surname)
                || person.Age < 0)
            {
                throw new InvalidDataException(
                    $"Person {person.Id} has invalid identity data.");
            }

            foreach (var component in
                person.Components)
            {
                if (string.IsNullOrWhiteSpace(
                        component.AssemblyName)
                    || string.IsNullOrWhiteSpace(
                        component.TypeName)
                    || string.IsNullOrWhiteSpace(
                        component.Json))
                {
                    throw new InvalidDataException(
                        $"Person {person.Id} contains an invalid component record.");
                }
            }
        }

        if (envelope.SelectedPersonId
                is Guid selectedId
            && !ids.Contains(
                selectedId))
        {
            throw new InvalidDataException(
                "Selected person is missing from the save file.");
        }

        if (envelope.ActiveControllerId
                is Guid activeId
            && !ids.Contains(
                activeId))
        {
            throw new InvalidDataException(
                "Active household head is missing from the save file.");
        }

        foreach (var queued in
            envelope.QueuedActions)
        {
            if (!ids.Contains(
                    queued.ActorId)
                || !ids.Contains(
                    queued.TargetId))
            {
                throw new InvalidDataException(
                    $"Queued action '{queued.ActionId}' " +
                    "references a missing person.");
            }
        }

        foreach (var biography in
            envelope.Biography)
        {
            if (!ids.Contains(
                biography.PersonId))
            {
                throw new InvalidDataException(
                    "Biography references a missing person.");
            }
        }
    }

    private static void DetectLegacyBrowserSave(
        string json)
    {
        using var document =
            JsonDocument.Parse(
                json);

        var root =
            document.RootElement;

        if (!root.TryGetProperty(
                "isDynastiaSave",
                out var marker)
            || marker.ValueKind
                != JsonValueKind.True)
        {
            return;
        }

        var hasFormat =
            root.TryGetProperty(
                "formatVersion",
                out _);

        var hasLegacyFamily =
            root.TryGetProperty(
                "family",
                out _);

        if (!hasFormat
            && hasLegacyFamily)
        {
            throw new InvalidDataException(
                "This is a Dynasty 4 browser save. " +
                "Desktop save/load uses a new modular schema; " +
                "legacy browser-save import is not implemented yet.");
        }
    }

    private static string Encrypt(
        string text)
    {
        var ciphered =
            SimpleCipher(
                text,
                CipherKey);

        var bytes =
            Encoding.UTF8.GetBytes(
                ciphered);

        return Convert.ToBase64String(
            bytes);
    }

    private static string Decrypt(
        string text)
    {
        var bytes =
            Convert.FromBase64String(
                text);

        var ciphered =
            Encoding.UTF8.GetString(
                bytes);

        return SimpleCipher(
            ciphered,
            CipherKey);
    }

    private static string SimpleCipher(
        string text,
        string key)
    {
        var result =
            new char[text.Length];

        for (var i = 0;
            i < text.Length;
            i++)
        {
            result[i] =
                (char)(
                    text[i]
                    ^ key[
                        i % key.Length]);
        }

        return new string(
            result);
    }

    private static string SanitizeFileNamePart(
        string value)
    {
        var invalid =
            Path.GetInvalidFileNameChars()
                .ToHashSet();

        var cleaned =
            new string(
                value
                    .Select(
                        character =>
                            invalid.Contains(
                                character)
                                ? '_'
                                : character)
                    .ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(
            cleaned)
                ? "dynasty"
                : cleaned;
    }

    private sealed record PreparedComponent(
        Type Type,
        object Value);

    private sealed record PreparedComponents(
        IReadOnlyDictionary<
            Guid,
            IReadOnlyList<PreparedComponent>>
            ByPerson);

    private sealed class DesktopSaveEnvelope
    {
        public bool IsDynastiaSave { get; set; }

        public int FormatVersion { get; set; }

        public string SaveKind { get; set; } =
            string.Empty;

        public string DynastySurname { get; set; } =
            string.Empty;

        public int Year { get; set; }

        public Guid? SelectedPersonId { get; set; }

        public Guid? ActiveControllerId { get; set; }

        public int AlbumYear { get; set; }

        public bool IsLivingFamilyView { get; set; } =
            true;

        public int DetailsTabIndex { get; set; }

        public List<PersonSaveData> People { get; set; } =
            [];

        public List<GameEventSaveData> Events { get; set; } =
            [];

        public List<QueuedActionSaveData> QueuedActions { get; set; } =
            [];

        public List<BiographyPersonSaveData> Biography { get; set; } =
            [];
    }

    private sealed class PersonSaveData
    {
        public Guid Id { get; set; }

        public string Name { get; set; } =
            string.Empty;

        public string Surname { get; set; } =
            string.Empty;

        public string? MaidenName { get; set; }

        public int Age { get; set; }

        public GameDate? BirthDate { get; set; }

        public GameDate? DeathDate { get; set; }

        public List<string> Tags { get; set; } =
            [];

        public List<ComponentSaveData> Components { get; set; } =
            [];
    }

    private sealed class ComponentSaveData
    {
        public string AssemblyName { get; set; } =
            string.Empty;

        public string TypeName { get; set; } =
            string.Empty;

        public string Json { get; set; } =
            string.Empty;
    }

    private sealed class GameEventSaveData
    {
        public string Type { get; set; } =
            string.Empty;

        public int Year { get; set; }

        public Guid? SubjectId { get; set; }

        public List<Guid> RelatedPersonIds { get; set; } =
            [];

        public Dictionary<string, string> Data { get; set; } =
            [];

        public static GameEventSaveData FromEvent(
            GameEvent gameEvent)
        {
            return new GameEventSaveData
            {
                Type =
                    gameEvent.Type,

                Year =
                    gameEvent.Year,

                SubjectId =
                    gameEvent.SubjectId,

                RelatedPersonIds =
                    gameEvent.RelatedPersonIds.ToList(),

                Data =
                    gameEvent.Data.ToDictionary(
                        pair =>
                            pair.Key,
                        pair =>
                            pair.Value)
            };
        }

        public GameEvent ToEvent()
        {
            return new GameEvent
            {
                Type =
                    Type,

                Year =
                    Year,

                SubjectId =
                    SubjectId,

                RelatedPersonIds =
                    RelatedPersonIds.ToList(),

                Data =
                    Data.ToDictionary(
                        pair =>
                            pair.Key,
                        pair =>
                            pair.Value)
            };
        }
    }

    private sealed class QueuedActionSaveData
    {
        public string ActionId { get; set; } =
            string.Empty;

        public string Label { get; set; } =
            string.Empty;

        public YearPhase Phase { get; set; }

        public Guid ActorId { get; set; }

        public Guid TargetId { get; set; }

        public static QueuedActionSaveData FromAction(
            QueuedActionInfo action)
        {
            return new QueuedActionSaveData
            {
                ActionId =
                    action.ActionId,

                Label =
                    action.Label,

                Phase =
                    action.Phase,

                ActorId =
                    action.ActorId,

                TargetId =
                    action.TargetId
            };
        }

        public QueuedActionInfo ToAction()
        {
            return new QueuedActionInfo(
                ActionId,
                Label,
                Phase,
                ActorId,
                TargetId);
        }
    }

    private sealed class BiographyPersonSaveData
    {
        public Guid PersonId { get; set; }

        public List<BiographyEntry> Entries { get; set; } =
            [];
    }
}
