using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dynastia.Contracts;

namespace Dynastia.App.Persistence;

public sealed partial class GameSaveService
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

}
