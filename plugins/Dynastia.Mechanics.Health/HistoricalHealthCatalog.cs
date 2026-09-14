using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class HistoricalHealthCatalog
{
    private const string VariantsPath =
        "Health/health_condition_variants.csv";

    private const string EraWeightsPath =
        "Health/health_condition_era_weights.csv";

    private readonly IReadOnlyDictionary<string, IReadOnlyList<ConditionVariantRule>> _variants;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ConditionWeightRule>> _weights;

    private HistoricalHealthCatalog(
        IReadOnlyDictionary<string, IReadOnlyList<ConditionVariantRule>> variants,
        IReadOnlyDictionary<string, IReadOnlyList<ConditionWeightRule>> weights)
    {
        _variants = variants;
        _weights = weights;
    }

    public static HistoricalHealthCatalog Load(
        IGameDataService data,
        IEnumerable<string> knownConditionIds)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(knownConditionIds);

        var known = knownConditionIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var variants = ParseVariants(data.ReadText(VariantsPath));
        var weights = ParseWeights(data.ReadText(EraWeightsPath));

        ValidateVariants(variants, known);
        ValidateWeights(weights, known);

        return new HistoricalHealthCatalog(
            GroupVariants(variants),
            GroupWeights(weights));
    }

    public string GetDisplayName(
        string conditionId,
        string baseName,
        int year)
    {
        if (!_variants.TryGetValue(conditionId, out var variants))
            return baseName;

        var effectiveYear = Math.Max(year, GameCalendarConfiguration.GameStartYear);
        return variants.LastOrDefault(rule => rule.Covers(effectiveYear))?.DisplayName
            ?? baseName;
    }

    public double GetWeightMultiplier(
        string conditionId,
        int year)
    {
        if (!_weights.TryGetValue(conditionId, out var rules))
            return 1.0;

        var effectiveYear = Math.Max(year, GameCalendarConfiguration.GameStartYear);
        return rules.LastOrDefault(rule => rule.Covers(effectiveYear))?.WeightMultiplier
            ?? 1.0;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ConditionVariantRule>> GroupVariants(
        IReadOnlyList<ConditionVariantRule> rules) =>
        rules.GroupBy(rule => rule.ConditionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ConditionVariantRule>)group.OrderBy(rule => rule.StartYear).ToList(),
                StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, IReadOnlyList<ConditionWeightRule>> GroupWeights(
        IReadOnlyList<ConditionWeightRule> rules) =>
        rules.GroupBy(rule => rule.ConditionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ConditionWeightRule>)group.OrderBy(rule => rule.StartYear).ToList(),
                StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<ConditionVariantRule> ParseVariants(string text)
    {
        var lines = SplitLines(text);
        const string expectedHeader = "ConditionId,StartYear,EndYear,DisplayName";
        if (lines.Length < 2 || !StripBom(lines[0]).Equals(expectedHeader, StringComparison.Ordinal))
            throw new InvalidDataException($"{VariantsPath} has an unexpected header or is empty.");

        var result = new List<ConditionVariantRule>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 4)
                throw new InvalidDataException($"Invalid {VariantsPath} row {index + 1}: expected 4 fields.");

            result.Add(new ConditionVariantRule(
                fields[0].Trim(),
                ParseInt(fields[1], VariantsPath, index),
                string.IsNullOrWhiteSpace(fields[2]) ? null : ParseInt(fields[2], VariantsPath, index),
                fields[3].Trim()));
        }

        return result;
    }

    private static IReadOnlyList<ConditionWeightRule> ParseWeights(string text)
    {
        var lines = SplitLines(text);
        const string expectedHeader = "ConditionId,StartYear,EndYear,WeightMultiplier";
        if (lines.Length < 2 || !StripBom(lines[0]).Equals(expectedHeader, StringComparison.Ordinal))
            throw new InvalidDataException($"{EraWeightsPath} has an unexpected header or is empty.");

        var result = new List<ConditionWeightRule>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 4)
                throw new InvalidDataException($"Invalid {EraWeightsPath} row {index + 1}: expected 4 fields.");

            result.Add(new ConditionWeightRule(
                fields[0].Trim(),
                ParseInt(fields[1], EraWeightsPath, index),
                string.IsNullOrWhiteSpace(fields[2]) ? null : ParseInt(fields[2], EraWeightsPath, index),
                ParseDouble(fields[3], EraWeightsPath, index)));
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
            ValidateRange(rule.ConditionId, rule.StartYear, rule.EndYear, VariantsPath);
        }

        ValidateNoOverlaps(
            rules.Select(rule => (rule.ConditionId, rule.StartYear, rule.EndYear)),
            VariantsPath);
    }

    private static void ValidateWeights(
        IReadOnlyList<ConditionWeightRule> rules,
        IReadOnlySet<string> knownIds)
    {
        foreach (var rule in rules)
        {
            if (!knownIds.Contains(rule.ConditionId))
                throw new InvalidDataException($"{EraWeightsPath}: unknown condition ID '{rule.ConditionId}'.");
            if (rule.WeightMultiplier <= 0)
                throw new InvalidDataException($"{EraWeightsPath}: multipliers must be greater than 0.");
            ValidateRange(rule.ConditionId, rule.StartYear, rule.EndYear, EraWeightsPath);
        }

        ValidateNoOverlaps(
            rules.Select(rule => (rule.ConditionId, rule.StartYear, rule.EndYear)),
            EraWeightsPath);
    }

    private static void ValidateRange(string id, int startYear, int? endYear, string path)
    {
        if (startYear < GameCalendarConfiguration.GameStartYear)
            throw new InvalidDataException($"{path}: '{id}' starts before {GameCalendarConfiguration.GameStartYear}.");
        if (endYear is int end && end < startYear)
            throw new InvalidDataException($"{path}: '{id}' has end year before start year.");
    }

    private static void ValidateNoOverlaps(
        IEnumerable<(string Id, int StartYear, int? EndYear)> rows,
        string path)
    {
        foreach (var group in rows.GroupBy(row => row.Id, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(row => row.StartYear).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                if (previous.EndYear is null || previous.EndYear.Value >= current.StartYear)
                    throw new InvalidDataException($"{path}: overlapping ranges for '{group.Key}'.");
            }
        }
    }

    private static string[] SplitLines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    private static string StripBom(string value) =>
        value.TrimStart('\uFEFF');

    private static int ParseInt(string value, string path, int rowIndex)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"Invalid year in {path} row {rowIndex + 2}.");
        return parsed;
    }

    private static double ParseDouble(string value, string path, int rowIndex)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"Invalid multiplier in {path} row {rowIndex + 2}.");
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

    private sealed record ConditionWeightRule(
        string ConditionId,
        int StartYear,
        int? EndYear,
        double WeightMultiplier)
    {
        public bool Covers(int year) =>
            year >= StartYear && (EndYear is null || year <= EndYear.Value);
    }
}
