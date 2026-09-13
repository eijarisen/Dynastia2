using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dynastia.Contracts;

namespace Dynastia.App.Persistence;

public sealed partial class GameSaveService
{
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

}
