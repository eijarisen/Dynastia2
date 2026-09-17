using System.Globalization;
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

        var values = CatalogValidation.DeserializeJson<List<string>>(
            this,
            relativePath);

        if (values.Count == 0)
        {
            throw CatalogValidation.Error(
                relativePath,
                "at least one list item",
                field: "Root",
                value: 0);
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
                throw CatalogValidation.Error(
                    relativePath,
                    "a weighted row in the form 'Name, weight'",
                    lineNumber,
                    field: "Row",
                    value: rawLine);
            }

            var value = line[..separator].Trim();
            var weightText = line[(separator + 1)..].Trim();

            if (value.Equals("name", StringComparison.OrdinalIgnoreCase)
                && weightText.Equals("weight", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (value.Length == 0)
            {
                throw CatalogValidation.Error(
                    relativePath,
                    "a non-empty name",
                    lineNumber,
                    field: "Name",
                    value: value);
            }

            if (!long.TryParse(
                weightText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var weight)
                || weight <= 0)
            {
                throw CatalogValidation.Error(
                    relativePath,
                    "a positive integer",
                    lineNumber,
                    field: "Weight",
                    value: weightText);
            }

            if (!seen.Add(value))
            {
                throw CatalogValidation.Error(
                    relativePath,
                    "a unique name",
                    lineNumber,
                    item: value,
                    field: "Name",
                    value: value);
            }

            result.Add(new WeightedStringEntry(value, weight));
        }

        if (result.Count == 0)
        {
            throw CatalogValidation.Error(
                relativePath,
                "at least one weighted item",
                field: "Rows",
                value: 0);
        }

        _weightedStringLists[relativePath] = result;
        return result;
    }

    public string ReadText(string relativePath)
    {
        return File.ReadAllText(
            ResolvePath(relativePath));
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
