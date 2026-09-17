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
        const string expectedHeader =
            "CrimeId,StartYear,EndYear,DisplayName,Description";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(expectedHeader, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                VariantsPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                expectedHeader);
        }

        var result = new List<CrimeVariantRule>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 5)
                throw CatalogValidation.FieldCount(VariantsPath, row, fields.Length, 5);

            result.Add(new CrimeVariantRule(
                row,
                fields[0].Trim(),
                CatalogValidation.ParseInt(VariantsPath, row, "StartYear", fields[1]),
                string.IsNullOrWhiteSpace(fields[2])
                    ? null
                    : CatalogValidation.ParseInt(VariantsPath, row, "EndYear", fields[2]),
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
            {
                throw CatalogValidation.Error(
                    VariantsPath,
                    "a CrimeId present in Common/crimes.json",
                    rule.SourceRow,
                    field: "CrimeId",
                    value: rule.CrimeId);
            }

            if (rule.StartYear < GameCalendarConfiguration.GameStartYear)
            {
                throw CatalogValidation.Error(
                    VariantsPath,
                    $"a year at or after {GameCalendarConfiguration.GameStartYear}",
                    rule.SourceRow,
                    rule.CrimeId,
                    "StartYear",
                    rule.StartYear);
            }

            if (rule.EndYear is int end && end < rule.StartYear)
            {
                throw CatalogValidation.Error(
                    VariantsPath,
                    $"a year at or after StartYear ({rule.StartYear})",
                    rule.SourceRow,
                    rule.CrimeId,
                    "EndYear",
                    end);
            }

            if (string.IsNullOrWhiteSpace(rule.DisplayName))
            {
                throw CatalogValidation.Error(
                    VariantsPath,
                    "a non-empty display name",
                    rule.SourceRow,
                    rule.CrimeId,
                    "DisplayName",
                    rule.DisplayName);
            }

            if (string.IsNullOrWhiteSpace(rule.Description))
            {
                throw CatalogValidation.Error(
                    VariantsPath,
                    "a non-empty description",
                    rule.SourceRow,
                    rule.CrimeId,
                    "Description",
                    rule.Description);
            }
        }

        foreach (var group in variants.GroupBy(
            rule => rule.CrimeId,
            StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(rule => rule.StartYear).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                if (previous.EndYear is null
                    || previous.EndYear.Value >= current.StartYear)
                {
                    throw CatalogValidation.Error(
                        VariantsPath,
                        $"a year range that does not overlap row {previous.SourceRow}",
                        current.SourceRow,
                        current.CrimeId,
                        "StartYear",
                        current.StartYear);
                }
            }
        }
    }

    private sealed record CrimeVariantRule(
        int SourceRow,
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
