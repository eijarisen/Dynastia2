using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class CrimeHistoricalCatalog
{
    private const string VariantsPath = "Justice/crime_variants.csv";

    private readonly IReadOnlyDictionary<string, IReadOnlyList<CrimeVariantRule>> _variants;

    private CrimeHistoricalCatalog(
        IReadOnlyDictionary<string, IReadOnlyList<CrimeVariantRule>> variants)
    {
        _variants = variants;
    }

    public static CrimeHistoricalCatalog Load(
        IGameDataService data,
        IEnumerable<string> knownCrimeIds)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(knownCrimeIds);

        var known = knownCrimeIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var variants = ParseVariants(data.ReadText(VariantsPath));
        ValidateVariants(variants, known);

        return new CrimeHistoricalCatalog(
            variants.GroupBy(rule => rule.CrimeId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<CrimeVariantRule>)group
                        .OrderBy(rule => rule.StartYear)
                        .ToList(),
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

    private static IReadOnlyList<CrimeVariantRule> ParseVariants(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string expectedHeader = "CrimeId,StartYear,EndYear,DisplayName,Description";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(expectedHeader, StringComparison.Ordinal))
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

    private static void ValidateVariants(
        IReadOnlyList<CrimeVariantRule> variants,
        IReadOnlySet<string> known)
    {
        foreach (var rule in variants)
        {
            if (!known.Contains(rule.CrimeId))
                throw new InvalidDataException($"{VariantsPath}: unknown crime ID '{rule.CrimeId}'.");
            if (rule.StartYear < GameCalendarConfiguration.GameStartYear)
                throw new InvalidDataException($"{VariantsPath}: invalid start year {rule.StartYear}.");
            if (rule.EndYear is int end && end < rule.StartYear)
                throw new InvalidDataException($"{VariantsPath}: end year precedes start year for '{rule.CrimeId}'.");
            if (string.IsNullOrWhiteSpace(rule.DisplayName) || string.IsNullOrWhiteSpace(rule.Description))
                throw new InvalidDataException($"{VariantsPath}: display name and description are required.");
        }

        foreach (var group in variants.GroupBy(rule => rule.CrimeId, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(rule => rule.StartYear).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                if (previous.EndYear is null || previous.EndYear.Value >= ordered[index].StartYear)
                    throw new InvalidDataException($"{VariantsPath}: overlapping variants for '{group.Key}'.");
            }
        }
    }

    private static int ParseInt(string value, string path, int rowIndex)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new InvalidDataException($"{path} row {rowIndex + 1}: '{value}' is not an integer.");
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
}

public sealed record CrimePresentation(
    string DisplayName,
    string Description);
