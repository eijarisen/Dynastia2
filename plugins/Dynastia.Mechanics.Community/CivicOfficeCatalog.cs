using System.Globalization;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal sealed class CivicOfficeCatalog
{
    private const string Path = "LocalSociety/civic_office_profiles.csv";
    private readonly IReadOnlyList<CivicOfficeProfileInfo> _profiles;

    private CivicOfficeCatalog(IReadOnlyList<CivicOfficeProfileInfo> profiles) =>
        _profiles = profiles;

    public CivicOfficeProfileInfo? Resolve(string polityId, int year) =>
        _profiles
            .Where(profile => profile.PolityId.Equals(polityId, StringComparison.OrdinalIgnoreCase)
                && year >= profile.StartYear
                && year <= profile.EndYear)
            .OrderByDescending(profile => profile.StartYear)
            .FirstOrDefault();

    public static CivicOfficeCatalog Load(IGameDataService data)
    {
        var rows = ParseCsv(data.ReadText(Path));
        var profiles = rows.Select(row => new CivicOfficeProfileInfo(
                Required(row, "PolityId"),
                ParseInt(row, "StartYear"),
                ParseInt(row, "EndYear"),
                Required(row, "MemberTitle"),
                Required(row, "HeadTitleSmallTown"),
                Required(row, "HeadTitleCity"),
                ParseInt(row, "MinimumAge"),
                Required(row, "AllowedSex"),
                ParseInt(row, "MinimumEducation"),
                ParseDouble(row, "MinimumRenown"),
                ParseDouble(row, "MinimumReputation"),
                ParseInt(row, "MinimumParticipation")))
            .ToArray();

        if (profiles.Length == 0)
            throw new InvalidDataException($"{Path}: no civic office profiles were loaded.");

        return new CivicOfficeCatalog(profiles);
    }

    private static string Required(IReadOnlyDictionary<string, string> row, string field) =>
        row.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidDataException($"{Path}: missing required field '{field}'.");

    private static int ParseInt(IReadOnlyDictionary<string, string> row, string field) =>
        int.TryParse(Required(row, field), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"{Path}: invalid integer in '{field}'.");

    private static double ParseDouble(IReadOnlyDictionary<string, string> row, string field) =>
        double.TryParse(Required(row, field), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"{Path}: invalid number in '{field}'.");

    private static IReadOnlyList<Dictionary<string, string>> ParseCsv(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return [];

        var headers = ParseCsvLine(lines[0].TrimStart('\uFEFF'));
        var result = new List<Dictionary<string, string>>();
        for (var index = 1; index < lines.Length; index++)
        {
            var values = ParseCsvLine(lines[index]);
            if (values.Count != headers.Count)
                throw new InvalidDataException($"{Path}: row {index + 1} has {values.Count} fields; expected {headers.Count}.");
            result.Add(headers.Select((header, fieldIndex) =>
                    new KeyValuePair<string, string>(header, values[fieldIndex]))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
        }
        return result;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
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
                continue;
            }

            if (character == ',' && !quoted)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }
            current.Append(character);
        }
        result.Add(current.ToString().Trim());
        return result;
    }
}
