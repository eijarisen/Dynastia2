using System.Globalization;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class TownInstitutionCareerCatalog
{
    private const string RequirementsPath =
        "TownLife/career_institution_requirements.csv";

    private const string CareersPath =
        "Career/careers.csv";

    private readonly IReadOnlyList<CareerInstitutionDisplayRule> _rules;

    private TownInstitutionCareerCatalog(
        IReadOnlyList<CareerInstitutionDisplayRule> rules)
    {
        _rules = rules;
    }

    public static TownInstitutionCareerCatalog Load(
        IGameDataService data)
    {
        var careerRows = ParseCsv(
            data.ReadText(CareersPath),
            CareersPath);

        var careers = careerRows.ToDictionary(
            row => Required(row, "Id", CareersPath),
            row => new CareerDisplayDefinition(
                Required(row, "Name", CareersPath),
                Optional(row, "Emoji") ?? "💼",
                ParseInt(row, "StartYear", CareersPath),
                ParseNullableInt(row, "EndYear", CareersPath)),
            StringComparer.OrdinalIgnoreCase);

        var rules = ParseCsv(
                data.ReadText(RequirementsPath),
                RequirementsPath)
            .Select(row =>
            {
                var careerId = Required(
                    row,
                    "CareerId",
                    RequirementsPath);

                if (!careers.TryGetValue(
                        careerId,
                        out var career))
                {
                    throw new InvalidDataException(
                        $"{RequirementsPath}: unknown CareerId '{careerId}'.");
                }

                return new CareerInstitutionDisplayRule(
                    careerId,
                    Required(row, "InstitutionId", RequirementsPath),
                    ParseInt(row, "MinimumTier", RequirementsPath),
                    career.Name,
                    career.Emoji,
                    career.StartYear,
                    career.EndYear);
            })
            .ToArray();

        return new TownInstitutionCareerCatalog(rules);
    }

    public IReadOnlyList<string> GetEnabledCareers(
        string institutionId,
        int tier,
        int year)
    {
        if (tier <= 0)
            return Array.Empty<string>();

        return _rules
            .Where(rule =>
                rule.InstitutionId.Equals(
                    institutionId,
                    StringComparison.OrdinalIgnoreCase)
                && tier >= rule.MinimumTier
                && year >= rule.StartYear
                && (rule.EndYear is null
                    || year <= rule.EndYear.Value))
            .OrderBy(rule => rule.MinimumTier)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(rule => $"{rule.Emoji} {rule.Name}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<Dictionary<string, string>> ParseCsv(
        string text,
        string path)
    {
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
            throw new InvalidDataException($"{path}: file is empty.");

        var headers = ParseCsvLine(lines[0].TrimStart('\uFEFF'));
        var rows = new List<Dictionary<string, string>>();

        for (var index = 1; index < lines.Length; index++)
        {
            var values = ParseCsvLine(lines[index]);
            if (values.Count != headers.Count)
            {
                throw new InvalidDataException(
                    $"{path}: row {index + 1} has {values.Count} fields; expected {headers.Count}.");
            }

            rows.Add(headers
                .Select((header, fieldIndex) =>
                    new KeyValuePair<string, string>(
                        header,
                        values[fieldIndex]))
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase));
        }

        return rows;
    }

    private static IReadOnlyList<string> ParseCsvLine(
        string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted
                    && index + 1 < line.Length
                    && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                quoted = !quoted;
                continue;
            }

            if (character == ',' && !quoted)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        fields.Add(current.ToString().Trim());
        return fields;
    }

    private static string Required(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path)
    {
        if (!row.TryGetValue(field, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"{path}: missing required field '{field}'.");
        }

        return value.Trim();
    }

    private static string? Optional(
        IReadOnlyDictionary<string, string> row,
        string field) =>
        row.TryGetValue(field, out var value)
        && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static int ParseInt(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path)
    {
        var value = Required(row, field, path);
        if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var result))
        {
            throw new InvalidDataException(
                $"{path}: field '{field}' has invalid integer '{value}'.");
        }

        return result;
    }

    private static int? ParseNullableInt(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path)
    {
        var value = Optional(row, field);
        if (value is null)
            return null;

        if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var result))
        {
            throw new InvalidDataException(
                $"{path}: field '{field}' has invalid integer '{value}'.");
        }

        return result;
    }

    private sealed record CareerDisplayDefinition(
        string Name,
        string Emoji,
        int StartYear,
        int? EndYear);

    private sealed record CareerInstitutionDisplayRule(
        string CareerId,
        string InstitutionId,
        int MinimumTier,
        string Name,
        string Emoji,
        int StartYear,
        int? EndYear);
}
