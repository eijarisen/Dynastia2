using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class TownEconomicStrengthCatalog
{
    private const string CareerMapPath = "TownLife/career_family_opportunity_map.csv";
    private const string HistoricalEffectsPath = "TownLife/historical_event_prosperity_effects.csv";

    private TownEconomicStrengthCatalog(
        IReadOnlyDictionary<string, IReadOnlyList<string>> careerFamilyTags,
        IReadOnlyList<HistoricalProsperityEffect> historicalEffects)
    {
        CareerFamilyTags = careerFamilyTags;
        HistoricalEffects = historicalEffects;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> CareerFamilyTags { get; }
    public IReadOnlyList<HistoricalProsperityEffect> HistoricalEffects { get; }

    public static TownEconomicStrengthCatalog Load(IGameDataService data)
    {
        var careerRows = ParseCsv(data.ReadText(CareerMapPath), CareerMapPath);
        var careerMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in careerRows)
        {
            var family = Required(row, "CareerFamily", CareerMapPath);
            if (!careerMap.TryAdd(family, ParseTags(row.GetValueOrDefault("OpportunityTags"))))
                throw new InvalidDataException($"{CareerMapPath}: duplicate CareerFamily '{family}'.");
        }

        var historicalRows = ParseCsv(data.ReadText(HistoricalEffectsPath), HistoricalEffectsPath);
        var effects = historicalRows
            .Select(row => new HistoricalProsperityEffect(
                Required(row, "HistoricalEventId", HistoricalEffectsPath),
                ParseInt(row, "ProsperityDelta", HistoricalEffectsPath),
                ParsePositiveInt(row, "RecoveryYears", HistoricalEffectsPath)))
            .ToArray();

        if (effects.Select(effect => effect.EventId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != effects.Length)
            throw new InvalidDataException($"{HistoricalEffectsPath}: HistoricalEventId values must be unique.");

        return new TownEconomicStrengthCatalog(careerMap, effects);
    }

    private static IReadOnlyList<string> ParseTags(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string Required(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path)
    {
        if (!row.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{path}: field '{field}' is required.");
        return value.Trim();
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path)
    {
        var value = Required(row, field, path);
        if (!int.TryParse(value, out var parsed))
            throw new InvalidDataException($"{path}: field '{field}' has invalid integer '{value}'.");
        return parsed;
    }

    private static int ParsePositiveInt(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path)
    {
        var parsed = ParseInt(row, field, path);
        if (parsed <= 0)
            throw new InvalidDataException($"{path}: field '{field}' must be positive.");
        return parsed;
    }

    private static IReadOnlyList<Dictionary<string, string>> ParseCsv(string text, string path)
    {
        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length == 0)
            throw new InvalidDataException($"{path}: CSV is empty.");

        var headers = ParseCsvLine(lines[0].TrimStart('\uFEFF'));
        var result = new List<Dictionary<string, string>>();
        for (var index = 1; index < lines.Length; index++)
        {
            var values = ParseCsvLine(lines[index]);
            if (values.Count != headers.Count)
                throw new InvalidDataException($"{path}: row {index + 1} has {values.Count} fields; expected {headers.Count}.");

            result.Add(headers
                .Select((header, column) => (header, value: values[column]))
                .ToDictionary(item => item.header, item => item.value, StringComparer.OrdinalIgnoreCase));
        }

        return result;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString().Trim());
        return values;
    }
}

internal sealed record HistoricalProsperityEffect(
    string EventId,
    int Delta,
    int RecoveryYears);
