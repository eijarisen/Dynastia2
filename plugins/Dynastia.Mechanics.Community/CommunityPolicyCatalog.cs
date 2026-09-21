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
    string? RequiredInstitutionId,
    string? RequiredOpportunityTag,
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
    string Description)
{
    public bool IsNoEffect => EffectKey.Equals("none", StringComparison.OrdinalIgnoreCase);
    public bool IsUnfavorable => Favorability.Equals("Unfavorable", StringComparison.OrdinalIgnoreCase);
    public bool IsFavorable => Favorability.Equals("Favorable", StringComparison.OrdinalIgnoreCase);
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
    private const string ArchetypesPath = "LocalSociety/connection_archetypes.csv";

    private readonly IReadOnlyDictionary<string, CommunityPolicyDefinition> _byId;
    private readonly IReadOnlyDictionary<string, CommunityConnectionArchetype> _archetypes;

    private CommunityPolicyCatalog(
        IReadOnlyList<CommunityPolicyDefinition> policies,
        IReadOnlyDictionary<string, CommunityConnectionArchetype> archetypes)
    {
        Policies = policies;
        _byId = policies.ToDictionary(policy => policy.Id, StringComparer.OrdinalIgnoreCase);
        _archetypes = archetypes;
    }

    public IReadOnlyList<CommunityPolicyDefinition> Policies { get; }

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

        var policies = ParseCsv(data.ReadText(PoliciesPath), PoliciesPath)
            .Select(row => new CommunityPolicyDefinition(
                Required(row, "PolicyId", PoliciesPath),
                Required(row, "DisplayName", PoliciesPath),
                ParseInt(row, "StartYear", PoliciesPath),
                ParseNullableInt(row, "EndYear", PoliciesPath),
                ParseSettlementClass(Required(row, "MinimumSettlementClass", PoliciesPath), PoliciesPath),
                Optional(row, "RequiredInstitutionId"),
                Optional(row, "RequiredOpportunityTag"),
                ParseNullableInt(row, "RequiredProsperityMax", PoliciesPath),
                SplitSet(Optional(row, "RequiredRegionIds")),
                Required(row, "Rarity", PoliciesPath),
                ParseDouble(row, "SelectionWeight", PoliciesPath),
                Required(row, "ImpactTier", PoliciesPath),
                ParseDouble(row, "BaseSupport", PoliciesPath),
                ParseInt(row, "DurationYears", PoliciesPath),
                Required(row, "EffectKey", PoliciesPath),
                ParseDouble(row, "Magnitude", PoliciesPath),
                Required(row, "Favorability", PoliciesPath),
                SplitList(Required(row, "ProposerArchetypes", PoliciesPath)),
                Required(row, "Description", PoliciesPath)))
            .ToArray();

        if (policies.Select(policy => policy.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != policies.Length)
            throw new InvalidDataException($"{PoliciesPath}: PolicyId values must be unique.");

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

        foreach (var policy in policies)
        {
            foreach (var archetypeId in policy.ProposerArchetypes)
            {
                if (!archetypes.ContainsKey(archetypeId))
                    throw new InvalidDataException($"{PoliciesPath}: policy '{policy.Id}' references unknown archetype '{archetypeId}'.");
            }
        }

        return new CommunityPolicyCatalog(policies, archetypes);
    }

    private static SettlementClass ParseSettlementClass(string value, string path) =>
        Enum.TryParse<SettlementClass>(value, ignoreCase: true, out var result)
            ? result
            : throw new InvalidDataException($"{path}: invalid settlement class '{value}'.");

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
