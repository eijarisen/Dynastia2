using System.Globalization;
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
            throw new InvalidDataException($"{VariantsPath} has an unexpected header or is empty.");

        var result = new List<ConditionVariantRule>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 4)
                throw new InvalidDataException($"Invalid {VariantsPath} row {index + 1}: expected 4 fields.");

            result.Add(new ConditionVariantRule(
                fields[0].Trim(),
                ParseInt(fields[1], index),
                string.IsNullOrWhiteSpace(fields[2]) ? null : ParseInt(fields[2], index),
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
                throw new InvalidDataException($"{VariantsPath}: unknown condition ID '{rule.ConditionId}'.");
            if (string.IsNullOrWhiteSpace(rule.DisplayName))
                throw new InvalidDataException($"{VariantsPath}: display name may not be blank.");
            if (rule.StartYear < GameCalendarConfiguration.GameStartYear)
                throw new InvalidDataException($"{VariantsPath}: '{rule.ConditionId}' starts before {GameCalendarConfiguration.GameStartYear}.");
            if (rule.EndYear is int end && end < rule.StartYear)
                throw new InvalidDataException($"{VariantsPath}: '{rule.ConditionId}' has end year before start year.");
        }

        foreach (var group in rules.GroupBy(rule => rule.ConditionId, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(rule => rule.StartYear).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                if (previous.EndYear is null || previous.EndYear.Value >= ordered[index].StartYear)
                    throw new InvalidDataException($"{VariantsPath}: overlapping ranges for '{group.Key}'.");
            }
        }
    }

    private static int ParseInt(string value, int rowIndex)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"Invalid year in {VariantsPath} row {rowIndex + 2}.");
        return parsed;
    }

    private sealed record ConditionVariantRule(
        string ConditionId,
        int StartYear,
        int? EndYear,
        string DisplayName)
    {
        public bool Covers(int year) =>
            year >= StartYear && (EndYear is null || year <= EndYear.Value);
    }
}
