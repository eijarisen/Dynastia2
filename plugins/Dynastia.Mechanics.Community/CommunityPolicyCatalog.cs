using System.Globalization;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal sealed record CommunityPolicyDefinition(
    string Id,
    string DisplayName,
    int StartYear,
    int? EndYear,
    SettlementClass MinimumSettlementClass,
    SettlementClass MaximumSettlementClass,
    string? RequiredInstitutionId,
    string? RequiredOpportunityTag,
    int? RequiredProsperityMin,
    int? RequiredProsperityMax,
    IReadOnlySet<string> RequiredRegionIds,
    string Rarity,
    double SelectionWeight,
    string ImpactTier,
    double BaseSupport,
    int DurationYears,
    string EffectKey,
    double Magnitude,
    string Favorability,
    IReadOnlyList<string> ProposerArchetypes,
    string Description,
    string EffectSummary)
{
    public bool IsNoEffect => EffectKey.Equals("none", StringComparison.OrdinalIgnoreCase);
    public bool IsUnfavorable => Favorability.Equals("Unfavorable", StringComparison.OrdinalIgnoreCase);
    public bool IsStrong => ImpactTier.Equals("Strong", StringComparison.OrdinalIgnoreCase);
}

internal sealed record CommunityConnectionArchetype(
    string Id,
    string DisplayOccupation,
    string MinimumWealthBand,
    string MaximumWealthBand,
    double TypicalRenown,
    double TypicalReputation,
    IReadOnlySet<string> PolicyThemes);

internal sealed class CommunityPolicyCatalog
{
    private const string PoliciesPath = "LocalSociety/community_policies.csv";
    private const string LegacyPoliciesPath = "LocalSociety/community_policies_legacy.csv";
    private const string ArchetypesPath = "LocalSociety/connection_archetypes.csv";

    private readonly IReadOnlyDictionary<string, CommunityPolicyDefinition> _byId;
    private readonly IReadOnlyDictionary<string, CommunityConnectionArchetype> _archetypes;

    private CommunityPolicyCatalog(
        IReadOnlyList<CommunityPolicyDefinition> policies,
        IReadOnlyList<CommunityPolicyDefinition> legacyPolicies,
        IReadOnlyDictionary<string, CommunityConnectionArchetype> archetypes)
    {
        Policies = policies;
        LegacyPolicies = legacyPolicies;
        _byId = policies
            .Concat(legacyPolicies)
            .ToDictionary(policy => policy.Id, StringComparer.OrdinalIgnoreCase);
        _archetypes = archetypes;
    }

    public IReadOnlyList<CommunityPolicyDefinition> Policies { get; }
    public IReadOnlyList<CommunityPolicyDefinition> LegacyPolicies { get; }

    public CommunityPolicyDefinition Get(string policyId) =>
        _byId.TryGetValue(policyId, out var policy)
            ? policy
            : throw new KeyNotFoundException($"Unknown community policy '{policyId}'.");

    public CommunityConnectionArchetype GetArchetype(string archetypeId) =>
        _archetypes.TryGetValue(archetypeId, out var archetype)
            ? archetype
            : throw new KeyNotFoundException($"Unknown community connection archetype '{archetypeId}'.");

    public static CommunityPolicyCatalog Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var policies = LoadPolicies(data.ReadText(PoliciesPath), PoliciesPath);
        var legacyPolicies = LoadPolicies(data.ReadText(LegacyPoliciesPath), LegacyPoliciesPath);
        var allPolicies = policies.Concat(legacyPolicies).ToArray();

        if (allPolicies.Select(policy => policy.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != allPolicies.Length)
            throw new InvalidDataException($"{PoliciesPath}/{LegacyPoliciesPath}: PolicyId values must be unique across current and legacy catalogues.");

        var archetypes = ParseCsv(data.ReadText(ArchetypesPath), ArchetypesPath)
            .Select(row => new CommunityConnectionArchetype(
                Required(row, "ArchetypeId", ArchetypesPath),
                Required(row, "DisplayOccupation", ArchetypesPath),
                Required(row, "MinimumWealthBand", ArchetypesPath),
                Required(row, "MaximumWealthBand", ArchetypesPath),
                ParseDouble(row, "TypicalRenown", ArchetypesPath),
                ParseDouble(row, "TypicalReputation", ArchetypesPath),
                SplitSet(Optional(row, "PolicyThemes"))))
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var policy in allPolicies)
        {
            foreach (var archetypeId in policy.ProposerArchetypes)
            {
                if (!archetypes.ContainsKey(archetypeId))
                    throw new InvalidDataException($"Community policy '{policy.Id}' references unknown archetype '{archetypeId}'.");
            }
        }

        return new CommunityPolicyCatalog(policies, legacyPolicies, archetypes);
    }

    private static IReadOnlyList<CommunityPolicyDefinition> LoadPolicies(string text, string path) =>
        ParseCsv(text, path)
            .Select(row => new CommunityPolicyDefinition(
                Required(row, "PolicyId", path),
                Required(row, "DisplayName", path),
                ParseInt(row, "StartYear", path),
                ParseNullableInt(row, "EndYear", path),
                ParseSettlementClass(Required(row, "MinimumSettlementClass", path), path),
                ParseOptionalSettlementClass(row, "MaximumSettlementClass", path) ?? SettlementClass.MajorCity,
                Optional(row, "RequiredInstitutionId"),
                Optional(row, "RequiredOpportunityTag"),
                ParseNullableInt(row, "RequiredProsperityMin", path),
                ParseNullableInt(row, "RequiredProsperityMax", path),
                SplitSet(Optional(row, "RequiredRegionIds")),
                Required(row, "Rarity", path),
                ParseDouble(row, "SelectionWeight", path),
                Required(row, "ImpactTier", path),
                ParseDouble(row, "BaseSupport", path),
                ParseInt(row, "DurationYears", path),
                Required(row, "EffectKey", path),
                ParseDouble(row, "Magnitude", path),
                Required(row, "Favorability", path),
                SplitList(Required(row, "ProposerArchetypes", path)),
                Required(row, "Description", path),
                Required(row, "EffectSummary", path)))
            .ToArray();

    private static SettlementClass ParseSettlementClass(string value, string path) =>
        Enum.TryParse<SettlementClass>(value, ignoreCase: true, out var result)
            ? result
            : throw new InvalidDataException($"{path}: invalid settlement class '{value}'.");

    private static SettlementClass? ParseOptionalSettlementClass(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path)
    {
        var value = Optional(row, field);
        return value is null
            ? null
            : ParseSettlementClass(value, path);
    }

    private static IReadOnlySet<string> SplitSet(string? value) =>
        new HashSet<string>(SplitList(value ?? string.Empty), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> SplitList(string value) =>
        value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Required(IReadOnlyDictionary<string, string> row, string field, string path)
    {
        if (!row.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{path}: missing required field '{field}'.");
        return value.Trim();
    }

    private static string? Optional(IReadOnlyDictionary<string, string> row, string field) =>
        row.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static int ParseInt(IReadOnlyDictionary<string, string> row, string field, string path)
    {
        var value = Required(row, field, path);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"{path}: field '{field}' has invalid integer '{value}'.");
    }

    private static int? ParseNullableInt(IReadOnlyDictionary<string, string> row, string field, string path)
    {
        var value = Optional(row, field);
        if (value is null)
            return null;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"{path}: field '{field}' has invalid integer '{value}'.");
    }

    private static double ParseDouble(IReadOnlyDictionary<string, string> row, string field, string path)
    {
        var value = Required(row, field, path);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"{path}: field '{field}' has invalid number '{value}'.");
    }

    private static IReadOnlyList<Dictionary<string, string>> ParseCsv(string text, string path)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
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
                throw new InvalidDataException($"{path}: row {index + 1} has {values.Count} fields; expected {headers.Count}.");

            rows.Add(headers.Select((header, fieldIndex) =>
                    new KeyValuePair<string, string>(header, values[fieldIndex]))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
        }
        return rows;
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
