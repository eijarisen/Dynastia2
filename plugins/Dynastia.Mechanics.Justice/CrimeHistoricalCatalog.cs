using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class CrimeHistoricalCatalog
{
    private const string VariantsPath =
        "Justice/crime_variants.csv";

    private const string EraWeightsPath =
        "Justice/crime_era_weights.csv";

    private readonly IReadOnlyDictionary<string, IReadOnlyList<CrimeVariantRule>> _variants;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CrimeWeightRule>> _weights;

    private CrimeHistoricalCatalog(
        IReadOnlyDictionary<string, IReadOnlyList<CrimeVariantRule>> variants,
        IReadOnlyDictionary<string, IReadOnlyList<CrimeWeightRule>> weights)
    {
        _variants = variants;
        _weights = weights;
    }

    public static CrimeHistoricalCatalog Load(
        IGameDataService data,
        IEnumerable<string> knownCrimeIds)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(knownCrimeIds);

        var known = knownCrimeIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var variants = ParseVariants(data.ReadText(VariantsPath));
        var weights = ParseWeights(data.ReadText(EraWeightsPath));
        ValidateVariants(variants, known);
        ValidateWeights(weights, known);

        return new CrimeHistoricalCatalog(
            variants.GroupBy(rule => rule.CrimeId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<CrimeVariantRule>)group.OrderBy(rule => rule.StartYear).ToList(),
                    StringComparer.OrdinalIgnoreCase),
            weights.GroupBy(rule => rule.CrimeId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<CrimeWeightRule>)group.OrderBy(rule => rule.StartYear).ToList(),
                    StringComparer.OrdinalIgnoreCase));
    }

    public CrimePresentation Resolve(CrimeDefinition crime, int year)
    {
        var effectiveYear = Math.Max(year, GameCalendarConfiguration.GameStartYear);
        if (_variants.TryGetValue(crime.Id, out var variants))
        {
            var variant = variants.LastOrDefault(rule => rule.Covers(effectiveYear));
            if (variant is not null)
                return new CrimePresentation(variant.DisplayName, variant.Description);
        }

        return new CrimePresentation(crime.Name, crime.Description);
    }

    public double GetWeightMultiplier(string crimeId, int year)
    {
        if (!_weights.TryGetValue(crimeId, out var rules))
            return 1.0;

        var effectiveYear = Math.Max(year, GameCalendarConfiguration.GameStartYear);
        return rules.LastOrDefault(rule => rule.Covers(effectiveYear))?.WeightMultiplier ?? 1.0;
    }

    private static IReadOnlyList<CrimeVariantRule> ParseVariants(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string expectedHeader = "CrimeId,StartYear,EndYear,DisplayName,Description";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(expectedHeader, StringComparison.Ordinal))
            throw new InvalidDataException($"{VariantsPath} has an unexpected header or is empty.");

        var result = new List<CrimeVariantRule>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 5)
                throw new InvalidDataException($"Invalid {VariantsPath} row {index + 1}: expected 5 fields.");

            result.Add(new CrimeVariantRule(
                fields[0].Trim(),
                ParseInt(fields[1], VariantsPath, index),
                string.IsNullOrWhiteSpace(fields[2]) ? null : ParseInt(fields[2], VariantsPath, index),
                fields[3].Trim(),
                fields[4].Trim()));
        }
        return result;
    }

    private static IReadOnlyList<CrimeWeightRule> ParseWeights(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string expectedHeader = "CrimeId,StartYear,EndYear,WeightMultiplier";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(expectedHeader, StringComparison.Ordinal))
            throw new InvalidDataException($"{EraWeightsPath} has an unexpected header or is empty.");

        var result = new List<CrimeWeightRule>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 4)
                throw new InvalidDataException($"Invalid {EraWeightsPath} row {index + 1}: expected 4 fields.");

            result.Add(new CrimeWeightRule(
                fields[0].Trim(),
                ParseInt(fields[1], EraWeightsPath, index),
                string.IsNullOrWhiteSpace(fields[2]) ? null : ParseInt(fields[2], EraWeightsPath, index),
                ParseDouble(fields[3], EraWeightsPath, index)));
        }
        return result;
    }

    private static void ValidateVariants(
        IReadOnlyList<CrimeVariantRule> rules,
        IReadOnlySet<string> knownIds)
    {
        foreach (var rule in rules)
        {
            if (!knownIds.Contains(rule.CrimeId))
                throw new InvalidDataException($"{VariantsPath}: unknown crime ID '{rule.CrimeId}'.");
            if (string.IsNullOrWhiteSpace(rule.DisplayName) || string.IsNullOrWhiteSpace(rule.Description))
                throw new InvalidDataException($"{VariantsPath}: display names and descriptions may not be blank.");
            ValidateRange(rule.CrimeId, rule.StartYear, rule.EndYear, VariantsPath);
        }
        ValidateNoOverlaps(rules.Select(rule => (rule.CrimeId, rule.StartYear, rule.EndYear)), VariantsPath);
    }

    private static void ValidateWeights(
        IReadOnlyList<CrimeWeightRule> rules,
        IReadOnlySet<string> knownIds)
    {
        foreach (var rule in rules)
        {
            if (!knownIds.Contains(rule.CrimeId))
                throw new InvalidDataException($"{EraWeightsPath}: unknown crime ID '{rule.CrimeId}'.");
            if (rule.WeightMultiplier <= 0)
                throw new InvalidDataException($"{EraWeightsPath}: multipliers must be greater than 0.");
            ValidateRange(rule.CrimeId, rule.StartYear, rule.EndYear, EraWeightsPath);
        }
        ValidateNoOverlaps(rules.Select(rule => (rule.CrimeId, rule.StartYear, rule.EndYear)), EraWeightsPath);
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

    private sealed record CrimeVariantRule(
        string CrimeId,
        int StartYear,
        int? EndYear,
        string DisplayName,
        string Description)
    {
        public bool Covers(int year) =>
            year >= StartYear && (EndYear is null || year <= EndYear.Value);
    }

    private sealed record CrimeWeightRule(
        string CrimeId,
        int StartYear,
        int? EndYear,
        double WeightMultiplier)
    {
        public bool Covers(int year) =>
            year >= StartYear && (EndYear is null || year <= EndYear.Value);
    }
}

public sealed record CrimePresentation(
    string DisplayName,
    string Description);
