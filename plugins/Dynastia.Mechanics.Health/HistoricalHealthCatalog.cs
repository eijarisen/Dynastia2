using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class HistoricalHealthCatalog
{
    private const string VariantsPath = "Health/health_condition_variants.csv";

    private readonly IReadOnlyDictionary<string, IReadOnlyList<ConditionVariantRule>> _variants;

    private HistoricalHealthCatalog(
        IReadOnlyDictionary<string, IReadOnlyList<ConditionVariantRule>> variants)
    {
        _variants = variants;
    }

    public static HistoricalHealthCatalog Load(
        IGameDataService data,
        IEnumerable<string> knownConditionIds)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(knownConditionIds);

        var known = knownConditionIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var variants = ParseVariants(data.ReadText(VariantsPath));
        ValidateVariants(variants, known);

        return new HistoricalHealthCatalog(
            variants.GroupBy(rule => rule.ConditionId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<ConditionVariantRule>)group.OrderBy(rule => rule.StartYear).ToList(),
                    StringComparer.OrdinalIgnoreCase));
    }

    public string GetDisplayName(string conditionId, string baseName, int year)
    {
        if (!_variants.TryGetValue(conditionId, out var variants))
            return baseName;

        var effectiveYear = Math.Max(year, GameCalendarConfiguration.GameStartYear);
        return variants.LastOrDefault(rule => rule.Covers(effectiveYear))?.DisplayName ?? baseName;
    }

    private static IReadOnlyList<ConditionVariantRule> ParseVariants(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string expectedHeader = "ConditionId,StartYear,EndYear,DisplayName";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(expectedHeader, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                VariantsPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                expectedHeader);
        }

        var result = new List<ConditionVariantRule>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 4)
                throw CatalogValidation.FieldCount(VariantsPath, row, fields.Length, 4);

            result.Add(new ConditionVariantRule(
                row,
                fields[0].Trim(),
                CatalogValidation.ParseInt(VariantsPath, row, "StartYear", fields[1]),
                string.IsNullOrWhiteSpace(fields[2])
                    ? null
                    : CatalogValidation.ParseInt(VariantsPath, row, "EndYear", fields[2]),
                fields[3].Trim()));
        }
        return result;
    }

    private static void ValidateVariants(
        IReadOnlyList<ConditionVariantRule> rules,
        IReadOnlySet<string> knownIds)
    {
        foreach (var rule in rules)
        {
            if (!knownIds.Contains(rule.ConditionId))
                throw CatalogValidation.Error(VariantsPath, "a ConditionId defined in the health catalog", rule.SourceRow, rule.ConditionId, "ConditionId", rule.ConditionId);
            if (string.IsNullOrWhiteSpace(rule.DisplayName))
                throw CatalogValidation.Error(VariantsPath, "a non-empty display name", rule.SourceRow, rule.ConditionId, "DisplayName", rule.DisplayName);
            if (rule.StartYear < GameCalendarConfiguration.GameStartYear)
                throw CatalogValidation.Error(VariantsPath, $"a year at or after {GameCalendarConfiguration.GameStartYear}", rule.SourceRow, rule.ConditionId, "StartYear", rule.StartYear);
            if (rule.EndYear is int end && end < rule.StartYear)
                throw CatalogValidation.Error(VariantsPath, $"a year at or after StartYear ({rule.StartYear})", rule.SourceRow, rule.ConditionId, "EndYear", end);
        }

        foreach (var group in rules.GroupBy(rule => rule.ConditionId, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(rule => rule.StartYear).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                if (previous.EndYear is null || previous.EndYear.Value >= current.StartYear)
                {
                    throw CatalogValidation.Error(
                        VariantsPath,
                        $"a StartYear after the previous range from row {previous.SourceRow}",
                        current.SourceRow,
                        current.ConditionId,
                        "StartYear",
                        current.StartYear);
                }
            }
        }
    }

    private sealed record ConditionVariantRule(
        int SourceRow,
        string ConditionId,
        int StartYear,
        int? EndYear,
        string DisplayName)
    {
        public bool Covers(int year) =>
            year >= StartYear && (EndYear is null || year <= EndYear.Value);
    }
}
