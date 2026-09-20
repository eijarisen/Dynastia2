using System.Globalization;
using System.Text;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

internal sealed class HeirloomEventCatalog
{
    private const string HistoricalPath = "Heirlooms/historical_event_heirlooms.csv";
    private const string RarePath = "Heirlooms/rare_event_heirlooms.csv";
    private const string CrimePath = "Heirlooms/crime_heirloom_chances.csv";
    private const string HobbyPath = "Heirlooms/hobby_heirloom_chances.csv";

    private HeirloomEventCatalog(
        IReadOnlyDictionary<string, HistoricalHeirloomRule> historical,
        IReadOnlyDictionary<string, RareEventHeirloomRule> rare,
        IReadOnlyDictionary<string, CrimeHeirloomRule> crimes,
        IReadOnlyDictionary<string, HobbyHeirloomRule> hobbies,
        IReadOnlyDictionary<string, string> historicalNames,
        IReadOnlyDictionary<string, string> rareNames)
    {
        Historical = historical;
        Rare = rare;
        Crimes = crimes;
        Hobbies = hobbies;
        HistoricalNames = historicalNames;
        RareNames = rareNames;
    }

    public IReadOnlyDictionary<string, HistoricalHeirloomRule> Historical { get; }
    public IReadOnlyDictionary<string, RareEventHeirloomRule> Rare { get; }
    public IReadOnlyDictionary<string, CrimeHeirloomRule> Crimes { get; }
    public IReadOnlyDictionary<string, HobbyHeirloomRule> Hobbies { get; }
    public IReadOnlyDictionary<string, string> HistoricalNames { get; }
    public IReadOnlyDictionary<string, string> RareNames { get; }

    public static HeirloomEventCatalog Load(
        IGameDataService data,
        HeirloomCatalog heirlooms)
    {
        var historical = ParseHistorical(data.ReadText(HistoricalPath));
        var rare = ParseRare(data.ReadText(RarePath));
        var crimes = ParseCrime(data.ReadText(CrimePath));
        var hobbies = ParseHobby(data.ReadText(HobbyPath));

        ValidateTemplates(heirlooms, historical.Values.SelectMany(rule => rule.TemplateIds), HistoricalPath);
        ValidateTemplates(heirlooms, rare.Values.SelectMany(rule => rule.TemplateIds), RarePath);
        ValidateTemplates(heirlooms, crimes.Values.SelectMany(rule => rule.TemplateIds), CrimePath);
        ValidateTemplates(heirlooms, hobbies.Values.SelectMany(rule => rule.TemplateIds), HobbyPath);

        var historicalNames = ReadIdNameMap(
            data.ReadText("HistoricalEvents/historical_events.csv"),
            "HistoricalEvents/historical_events.csv",
            "Id",
            "DisplayName");
        var rareNames = ReadIdNameMap(
            data.ReadText("RareEvents/rare_events.csv"),
            "RareEvents/rare_events.csv",
            "EventId",
            "Name");
        var hobbyNames = ReadIdNameMap(
            data.ReadText("Hobbies/hobbies.csv"),
            "Hobbies/hobbies.csv",
            "Id",
            "Name");
        var crimeIds = ReadCrimeIds(data.ReadText("Common/crimes.json"));

        ValidateKnown(historical.Keys, historicalNames.Keys, HistoricalPath, "HistoricalEventId");
        ValidateKnown(rare.Keys, rareNames.Keys, RarePath, "RareEventId");
        ValidateKnown(crimes.Keys, crimeIds, CrimePath, "CrimeId");
        ValidateKnown(hobbies.Keys, hobbyNames.Keys, HobbyPath, "HobbyId");

        return new HeirloomEventCatalog(
            historical,
            rare,
            crimes,
            hobbies,
            historicalNames,
            rareNames);
    }

    private static IReadOnlyDictionary<string, HistoricalHeirloomRule> ParseHistorical(string text)
    {
        var rows = ParseCsv(text, HistoricalPath);
        ValidateHeader(rows, HistoricalPath,
            "HistoricalEventId", "ChanceIfHouseholdDirectlyAffected", "TemplateIds");
        var result = new Dictionary<string, HistoricalHeirloomRule>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < rows.Count; index++)
        {
            var id = RequireId(rows[index][0], HistoricalPath, index + 1, "HistoricalEventId");
            var chance = ParseChance(rows[index][1], HistoricalPath, index + 1);
            var rule = new HistoricalHeirloomRule(id, chance, SplitIds(rows[index][2], HistoricalPath, index + 1));
            if (!result.TryAdd(id, rule))
                throw new InvalidDataException($"{HistoricalPath}: duplicate HistoricalEventId '{id}'.");
        }
        return result;
    }

    private static IReadOnlyDictionary<string, RareEventHeirloomRule> ParseRare(string text)
    {
        var rows = ParseCsv(text, RarePath);
        ValidateHeader(rows, RarePath, "RareEventId", "Chance", "TemplateIds");
        var result = new Dictionary<string, RareEventHeirloomRule>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < rows.Count; index++)
        {
            var id = RequireId(rows[index][0], RarePath, index + 1, "RareEventId");
            var chance = ParseChance(rows[index][1], RarePath, index + 1);
            var rule = new RareEventHeirloomRule(id, chance, SplitIds(rows[index][2], RarePath, index + 1));
            if (!result.TryAdd(id, rule))
                throw new InvalidDataException($"{RarePath}: duplicate RareEventId '{id}'.");
        }
        return result;
    }

    private static IReadOnlyDictionary<string, CrimeHeirloomRule> ParseCrime(string text)
    {
        var rows = ParseCsv(text, CrimePath);
        ValidateHeader(rows, CrimePath, "CrimeId", "ChanceOnSuccessfulUncaughtCrime", "TemplateIds");
        var result = new Dictionary<string, CrimeHeirloomRule>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < rows.Count; index++)
        {
            var id = RequireId(rows[index][0], CrimePath, index + 1, "CrimeId");
            var chance = ParseChance(rows[index][1], CrimePath, index + 1);
            var rule = new CrimeHeirloomRule(id, chance, SplitIds(rows[index][2], CrimePath, index + 1));
            if (!result.TryAdd(id, rule))
                throw new InvalidDataException($"{CrimePath}: duplicate CrimeId '{id}'.");
        }
        return result;
    }

    private static IReadOnlyDictionary<string, HobbyHeirloomRule> ParseHobby(string text)
    {
        var rows = ParseCsv(text, HobbyPath);
        ValidateHeader(rows, HobbyPath, "HobbyId", "MinimumAge", "AnnualChance", "TemplateIds");
        var result = new Dictionary<string, HobbyHeirloomRule>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < rows.Count; index++)
        {
            var id = RequireId(rows[index][0], HobbyPath, index + 1, "HobbyId");
            if (!int.TryParse(rows[index][1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minimumAge)
                || minimumAge < 0)
            {
                throw new InvalidDataException($"{HobbyPath}: row {index + 1} has invalid MinimumAge '{rows[index][1]}'.");
            }
            var chance = ParseChance(rows[index][2], HobbyPath, index + 1);
            var rule = new HobbyHeirloomRule(id, minimumAge, chance, SplitIds(rows[index][3], HobbyPath, index + 1));
            if (!result.TryAdd(id, rule))
                throw new InvalidDataException($"{HobbyPath}: duplicate HobbyId '{id}'.");
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> ReadIdNameMap(
        string text,
        string path,
        string idHeader,
        string nameHeader)
    {
        var rows = ParseCsv(text, path);
        if (rows.Count < 2)
            throw new InvalidDataException($"{path}: empty data.");
        var idIndex = rows[0].ToList().FindIndex(value => value.Equals(idHeader, StringComparison.Ordinal));
        var nameIndex = rows[0].ToList().FindIndex(value => value.Equals(nameHeader, StringComparison.Ordinal));
        if (idIndex < 0 || nameIndex < 0)
            throw new InvalidDataException($"{path}: required columns '{idHeader}'/'{nameHeader}' are missing.");

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Skip(1))
        {
            if (row.Count <= Math.Max(idIndex, nameIndex))
                continue;
            var id = row[idIndex].Trim();
            if (!string.IsNullOrWhiteSpace(id))
                result[id] = row[nameIndex].Trim();
        }
        return result;
    }

    private static IReadOnlyCollection<string> ReadCrimeIds(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Common/crimes.json: expected an array.");
        return document.RootElement.EnumerateArray()
            .Select(item => item.TryGetProperty("id", out var property) ? property.GetString() : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateKnown(
        IEnumerable<string> mappedIds,
        IEnumerable<string> knownIds,
        string path,
        string field)
    {
        var known = knownIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = mappedIds.FirstOrDefault(id => !known.Contains(id));
        if (unknown is not null)
            throw new InvalidDataException($"{path}: unknown {field} '{unknown}'.");
    }

    private static void ValidateTemplates(
        HeirloomCatalog catalog,
        IEnumerable<string> templateIds,
        string path)
    {
        foreach (var templateId in templateIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                _ = catalog.GetTemplate(templateId);
            }
            catch (KeyNotFoundException exception)
            {
                throw new InvalidDataException($"{path}: unknown TemplateId '{templateId}'.", exception);
            }
        }
    }

    private static string RequireId(string raw, string path, int row, string field)
    {
        var value = raw.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"{path}: row {row} has blank {field}.")
            : value;
    }

    private static double ParseChance(string raw, string path, int row)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var chance)
            || chance is < 0 or > 1)
        {
            throw new InvalidDataException($"{path}: row {row} has invalid chance '{raw}'.");
        }
        return chance;
    }

    private static IReadOnlyList<string> SplitIds(string raw, string path, int row)
    {
        var values = raw.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (values.Length == 0)
            throw new InvalidDataException($"{path}: row {row} has no TemplateIds.");
        return values;
    }

    private static void ValidateHeader(
        IReadOnlyList<IReadOnlyList<string>> rows,
        string path,
        params string[] expected)
    {
        if (rows.Count < 2 || !rows[0].SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException($"{path}: unexpected header or empty data.");
        foreach (var row in rows.Skip(1))
        {
            if (row.Count != expected.Length)
                throw new InvalidDataException($"{path}: row has {row.Count} fields, expected {expected.Length}.");
        }
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseCsv(string text, string path)
    {
        var rows = new List<IReadOnlyList<string>>();
        var currentRow = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else quoted = false;
                }
                else field.Append(ch);
                continue;
            }

            if (ch == '"') quoted = true;
            else if (ch == ',')
            {
                currentRow.Add(field.ToString().TrimStart('\uFEFF'));
                field.Clear();
            }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                currentRow.Add(field.ToString().TrimStart('\uFEFF'));
                field.Clear();
                if (currentRow.Any(value => value.Length > 0)) rows.Add(currentRow.ToList());
                currentRow.Clear();
            }
            else field.Append(ch);
        }

        if (field.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(field.ToString().TrimStart('\uFEFF'));
            if (currentRow.Any(value => value.Length > 0)) rows.Add(currentRow);
        }

        if (quoted)
            throw new InvalidDataException($"{path}: unterminated quoted CSV field.");
        return rows;
    }
}

internal sealed record HistoricalHeirloomRule(
    string EventId,
    double Chance,
    IReadOnlyList<string> TemplateIds);

internal sealed record RareEventHeirloomRule(
    string EventId,
    double Chance,
    IReadOnlyList<string> TemplateIds);

internal sealed record CrimeHeirloomRule(
    string CrimeId,
    double Chance,
    IReadOnlyList<string> TemplateIds);

internal sealed record HobbyHeirloomRule(
    string HobbyId,
    int MinimumAge,
    double AnnualChance,
    IReadOnlyList<string> TemplateIds);
