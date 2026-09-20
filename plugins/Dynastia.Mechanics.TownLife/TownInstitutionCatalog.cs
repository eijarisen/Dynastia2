using System.Globalization;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class TownInstitutionCatalog
{
    private const string TypesPath = "TownLife/institution_types.csv";
    private const string TierNamesPath = "TownLife/institution_tier_names.csv";
    private const string InferencePath = "TownLife/institution_inference_rules.csv";
    private const string OverridesPath = "TownLife/institution_overrides.csv";

    private TownInstitutionCatalog(
        IReadOnlyList<InstitutionTypeDefinition> institutionTypes,
        IReadOnlyList<InstitutionTierNameDefinition> tierNames,
        IReadOnlyList<InstitutionInferenceRule> inferenceRules,
        IReadOnlyList<InstitutionOverrideRule> overrides)
    {
        InstitutionTypes = institutionTypes;
        TierNames = tierNames;
        InferenceRules = inferenceRules;
        Overrides = overrides;
    }

    public IReadOnlyList<InstitutionTypeDefinition> InstitutionTypes { get; }
    public IReadOnlyList<InstitutionTierNameDefinition> TierNames { get; }
    public IReadOnlyList<InstitutionInferenceRule> InferenceRules { get; }
    public IReadOnlyList<InstitutionOverrideRule> Overrides { get; }

    public static TownInstitutionCatalog Load(
        IGameDataService data,
        IHistoricalTownCatalog towns)
    {
        var typeRows = ParseCsv(data.ReadText(TypesPath), TypesPath);
        var types = typeRows
            .Select(row => new InstitutionTypeDefinition(
                Required(row, "InstitutionId", TypesPath),
                Required(row, "DisplayName", TypesPath),
                ParseInt(row, "MaxTier", TypesPath, min: 1),
                Required(row, "Purpose", TypesPath)))
            .ToArray();

        var typeById = new Dictionary<string, InstitutionTypeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in types)
        {
            if (!typeById.TryAdd(type.Id, type))
                throw Error(TypesPath, $"duplicate InstitutionId '{type.Id}'");
        }

        var tierNames = ParseCsv(data.ReadText(TierNamesPath), TierNamesPath)
            .Select(row =>
            {
                var institutionId = Required(row, "InstitutionId", TierNamesPath);
                var tier = ParseInt(row, "Tier", TierNamesPath, min: 1);
                ValidateTier(typeById, institutionId, tier, TierNamesPath);
                return new InstitutionTierNameDefinition(
                    institutionId,
                    tier,
                    ParseInt(row, "StartYear", TierNamesPath, min: 1),
                    ParseNullableInt(row, "EndYear", TierNamesPath),
                    Required(row, "DisplayName", TierNamesPath));
            })
            .ToArray();

        var seenRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inference = ParseCsv(data.ReadText(InferencePath), InferencePath)
            .Select(row =>
            {
                var ruleId = Required(row, "RuleId", InferencePath);
                if (!seenRuleIds.Add(ruleId))
                    throw Error(InferencePath, $"duplicate RuleId '{ruleId}'");

                var institutionId = Required(row, "InstitutionId", InferencePath);
                var tier = ParseInt(row, "Tier", InferencePath, min: 1);
                ValidateTier(typeById, institutionId, tier, InferencePath);

                var startYear = ParseInt(row, "StartYear", InferencePath, min: 1);
                var endYear = ParseNullableInt(row, "EndYear", InferencePath);
                ValidateRange(startYear, endYear, InferencePath, ruleId);

                return new InstitutionInferenceRule(
                    ruleId,
                    institutionId,
                    tier,
                    startYear,
                    endYear,
                    ParseInt(row, "MinPopulation", InferencePath, min: 0),
                    SplitTags(Optional(row, "RequiredAnyOpportunityTags")));
            })
            .ToArray();

        var overrides = ParseCsv(data.ReadText(OverridesPath), OverridesPath)
            .Select(row =>
            {
                var placeId = Required(row, "PlaceId", OverridesPath);
                if (!towns.PermanentPlaceIds.Contains(placeId, StringComparer.OrdinalIgnoreCase))
                    throw Error(OverridesPath, $"unknown PlaceId '{placeId}'");

                var institutionId = Required(row, "InstitutionId", OverridesPath);
                var tier = ParseInt(row, "Tier", OverridesPath, min: 0);
                ValidateTier(typeById, institutionId, tier, OverridesPath, allowZero: true);

                var startYear = ParseInt(row, "StartYear", OverridesPath, min: 1);
                var endYear = ParseNullableInt(row, "EndYear", OverridesPath);
                ValidateRange(startYear, endYear, OverridesPath, $"{placeId}/{institutionId}");

                var mode = Required(row, "Mode", OverridesPath);
                if (!mode.Equals("MinimumTier", StringComparison.OrdinalIgnoreCase)
                    && !mode.Equals("ExactTier", StringComparison.OrdinalIgnoreCase))
                {
                    throw Error(OverridesPath, $"unsupported override mode '{mode}'");
                }

                return new InstitutionOverrideRule(
                    placeId,
                    institutionId,
                    mode,
                    tier,
                    startYear,
                    endYear,
                    Optional(row, "Reason"));
            })
            .ToArray();

        ValidateOverrideOverlaps(overrides);

        return new TownInstitutionCatalog(types, tierNames, inference, overrides);
    }

    public string ResolveTierName(
        string institutionId,
        int tier,
        int year)
    {
        if (tier <= 0)
            return "Unavailable";

        var match = TierNames
            .Where(row =>
                row.InstitutionId.Equals(institutionId, StringComparison.OrdinalIgnoreCase)
                && row.Tier == tier
                && IsActive(year, row.StartYear, row.EndYear))
            .OrderByDescending(row => row.StartYear)
            .FirstOrDefault();

        return match?.DisplayName
            ?? InstitutionTypes.First(type => type.Id.Equals(institutionId, StringComparison.OrdinalIgnoreCase)).DisplayName;
    }

    private static void ValidateOverrideOverlaps(
        IReadOnlyList<InstitutionOverrideRule> overrides)
    {
        foreach (var group in overrides.GroupBy(
                     rule => $"{rule.PlaceId}\u001f{rule.InstitutionId}",
                     StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(rule => rule.StartYear).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                var previousEnd = ordered[index - 1].EndYear ?? int.MaxValue;
                if (ordered[index].StartYear <= previousEnd)
                {
                    throw Error(
                        OverridesPath,
                        $"overlapping override rows for {ordered[index].PlaceId}/{ordered[index].InstitutionId}");
                }
            }
        }
    }

    private static bool IsActive(int year, int startYear, int? endYear) =>
        year >= startYear && (endYear is null || year <= endYear.Value);

    private static void ValidateTier(
        IReadOnlyDictionary<string, InstitutionTypeDefinition> types,
        string institutionId,
        int tier,
        string path,
        bool allowZero = false)
    {
        if (!types.TryGetValue(institutionId, out var type))
            throw Error(path, $"unknown InstitutionId '{institutionId}'");

        var minimum = allowZero ? 0 : 1;
        if (tier < minimum || tier > type.MaxTier)
            throw Error(path, $"invalid tier {tier} for institution '{institutionId}' (max {type.MaxTier})");
    }

    private static void ValidateRange(
        int startYear,
        int? endYear,
        string path,
        string item)
    {
        if (endYear is not null && endYear.Value < startYear)
            throw Error(path, $"invalid year range for '{item}'");
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path,
        int min)
    {
        var text = Required(row, field, path);
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value < min)
        {
            throw Error(path, $"field '{field}' has invalid value '{text}'");
        }

        return value;
    }

    private static int? ParseNullableInt(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path)
    {
        var text = Optional(row, field);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw Error(path, $"field '{field}' has invalid value '{text}'");

        return value;
    }

    private static string Required(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path)
    {
        var value = Optional(row, field);
        if (string.IsNullOrWhiteSpace(value))
            throw Error(path, $"field '{field}' is required");
        return value;
    }

    private static string Optional(
        IReadOnlyDictionary<string, string> row,
        string field) =>
        row.TryGetValue(field, out var value) ? value.Trim() : string.Empty;

    private static IReadOnlyList<string> SplitTags(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? Array.Empty<string>()
            : text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static InvalidDataException Error(string path, string message) =>
        new($"{path}: {message}");

    private static IReadOnlyList<Dictionary<string, string>> ParseCsv(
        string text,
        string path)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            throw Error(path, "file is empty");

        var headers = ParseCsvLine(lines[0].TrimStart('\uFEFF'));
        var rows = new List<Dictionary<string, string>>();

        for (var index = 1; index < lines.Length; index++)
        {
            var values = ParseCsvLine(lines[index]);
            if (values.Count != headers.Count)
                throw Error(path, $"line {index + 1} has {values.Count} values; expected {headers.Count}");

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var column = 0; column < headers.Count; column++)
                row[headers[column]] = values[column];
            rows.Add(row);
        }

        return rows;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
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

        if (quoted)
            throw new InvalidDataException("CSV row contains an unterminated quoted field.");

        values.Add(current.ToString().Trim());
        return values;
    }

    internal sealed record InstitutionTypeDefinition(
        string Id,
        string DisplayName,
        int MaxTier,
        string Purpose);

    internal sealed record InstitutionTierNameDefinition(
        string InstitutionId,
        int Tier,
        int StartYear,
        int? EndYear,
        string DisplayName);

    internal sealed record InstitutionInferenceRule(
        string RuleId,
        string InstitutionId,
        int Tier,
        int StartYear,
        int? EndYear,
        int MinPopulation,
        IReadOnlyList<string> RequiredAnyOpportunityTags);

    internal sealed record InstitutionOverrideRule(
        string PlaceId,
        string InstitutionId,
        string Mode,
        int Tier,
        int StartYear,
        int? EndYear,
        string Reason);
}
