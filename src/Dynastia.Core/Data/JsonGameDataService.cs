using System.Globalization;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Core.Data;

public sealed class JsonGameDataService : IGameDataService
{
    private readonly string _baseDirectory;

    private readonly Dictionary<string, IReadOnlyList<string>> _stringLists =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IReadOnlyList<WeightedStringEntry>>
        _weightedStringLists =
            new(StringComparer.OrdinalIgnoreCase);

    public JsonGameDataService(string baseDirectory)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory);
    }

    public IReadOnlyList<string> GetStringList(string relativePath)
    {
        if (_stringLists.TryGetValue(relativePath, out var cached))
            return cached;

        var fullPath = ResolvePath(relativePath);

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

    public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(
        string relativePath)
    {
        if (_weightedStringLists.TryGetValue(relativePath, out var cached))
            return cached;

        var fullPath = ResolvePath(relativePath);
        var result = new List<WeightedStringEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var lineNumber = 0;

        foreach (var rawLine in File.ReadLines(fullPath))
        {
            lineNumber++;

            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.LastIndexOf(',');

            if (separator <= 0 || separator == line.Length - 1)
            {
                throw new InvalidDataException(
                    $"{relativePath}:{lineNumber}: expected 'Name, weight'.");
            }

            var value = line[..separator].Trim();
            var weightText = line[(separator + 1)..].Trim();

            // Optional conventional CSV header.
            if (value.Equals("name", StringComparison.OrdinalIgnoreCase)
                && weightText.Equals("weight", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (value.Length == 0)
            {
                throw new InvalidDataException(
                    $"{relativePath}:{lineNumber}: name cannot be empty.");
            }

            if (!long.TryParse(
                weightText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var weight)
                || weight <= 0)
            {
                throw new InvalidDataException(
                    $"{relativePath}:{lineNumber}: weight must be a positive integer.");
            }

            if (!seen.Add(value))
            {
                throw new InvalidDataException(
                    $"{relativePath}:{lineNumber}: duplicate entry '{value}'.");
            }

            result.Add(new WeightedStringEntry(value, weight));
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException(
                $"Weighted game data list is empty: {relativePath}");
        }

        _weightedStringLists[relativePath] = result;
        return result;
    }

    private string ResolvePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

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

        return fullPath;
    }
}
