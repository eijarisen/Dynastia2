using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Core.Data;

public sealed class JsonGameDataService : IGameDataService
{
    private readonly string _baseDirectory;
    private readonly Dictionary<string, IReadOnlyList<string>> _stringLists =
        new(StringComparer.OrdinalIgnoreCase);

    public JsonGameDataService(string baseDirectory)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory);
    }

    public IReadOnlyList<string> GetStringList(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (_stringLists.TryGetValue(relativePath, out var cached))
            return cached;

        var fullPath = Path.GetFullPath(
            Path.Combine(_baseDirectory, relativePath));

        var allowedRoot =
            _baseDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
            allowedRoot,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Data path '{relativePath}' escapes the Data directory.");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Game data file was not found: {relativePath}",
                fullPath);
        }

        var values = JsonSerializer.Deserialize<List<string>>(
            File.ReadAllText(fullPath))
            ?? throw new InvalidDataException(
                $"Could not read string-list data: {relativePath}");

        if (values.Count == 0)
        {
            throw new InvalidDataException(
                $"Game data list is empty: {relativePath}");
        }

        _stringLists[relativePath] = values;

        return values;
    }
}
