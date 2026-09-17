using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class HistoricalCareerPresentationCatalog
{
    private const string CareerVariantsPath =
        "Career/career_title_variants.csv";

    private const string StatusVariantsPath =
        "Common/person_status_variants.csv";

    private static readonly IReadOnlySet<string> KnownStatusIds =
        new HashSet<string>(
            new[]
            {
                "status.preschool",
                "status.student",
                "status.unemployed",
                "status.housewife",
                "role.nanny",
                "role.family_nanny"
            },
            StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<CareerTitleVariant>> _careerVariants;

    private readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<PersonStatusVariant>> _statusVariants;

    private HistoricalCareerPresentationCatalog(
        IReadOnlyDictionary<
            string,
            IReadOnlyList<CareerTitleVariant>> careerVariants,
        IReadOnlyDictionary<
            string,
            IReadOnlyList<PersonStatusVariant>> statusVariants)
    {
        _careerVariants = careerVariants;
        _statusVariants = statusVariants;
    }

    public static HistoricalCareerPresentationCatalog Load(
        IGameDataService data,
        IEnumerable<string> knownCareerIds)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(knownCareerIds);

        var knownCareers = knownCareerIds.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        var careerVariants = ParseCareerVariants(
            data.ReadText(CareerVariantsPath));

        var statusVariants = ParseStatusVariants(
            data.ReadText(StatusVariantsPath));

        ValidateCareerVariants(
            careerVariants,
            knownCareers);

        ValidateStatusVariants(
            statusVariants);

        return new HistoricalCareerPresentationCatalog(
            careerVariants
                .GroupBy(
                    variant => variant.CareerId,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<CareerTitleVariant>)
                        group.OrderBy(variant => variant.StartYear).ToList(),
                    StringComparer.OrdinalIgnoreCase),
            statusVariants
                .GroupBy(
                    variant => variant.StatusId,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<PersonStatusVariant>)
                        group.OrderBy(variant => variant.StartYear).ToList(),
                    StringComparer.OrdinalIgnoreCase));
    }

    public string ResolveCareerName(
        string careerId,
        string baseName,
        int year)
    {
        var variant = FindCareerVariant(careerId, year);
        return variant?.DisplayName ?? baseName;
    }

    public string ResolveCareerTitle(
        string careerId,
        int jobLevel,
        string baseTitle,
        int year)
    {
        var variant = FindCareerVariant(careerId, year);
        if (variant is null)
            return baseTitle;

        return jobLevel switch
        {
            1 => variant.Level1Title ?? baseTitle,
            2 => variant.Level2Title ?? baseTitle,
            3 => variant.Level3Title ?? baseTitle,
            4 => variant.Level4Title ?? baseTitle,
            5 => variant.Level5Title ?? baseTitle,
            _ => baseTitle
        };
    }

    public string ResolveStatus(
        string statusId,
        string baseLabel,
        int year)
    {
        if (!_statusVariants.TryGetValue(
                statusId,
                out var variants))
        {
            return baseLabel;
        }

        var effectiveYear = Math.Max(
            year,
            GameCalendarConfiguration.GameStartYear);

        return variants.LastOrDefault(
                variant => variant.Covers(effectiveYear))
            ?.Label
            ?? baseLabel;
    }

    private CareerTitleVariant? FindCareerVariant(
        string careerId,
        int year)
    {
        if (!_careerVariants.TryGetValue(
                careerId,
                out var variants))
        {
            return null;
        }

        var effectiveYear = Math.Max(
            year,
            GameCalendarConfiguration.GameStartYear);

        return variants.LastOrDefault(
            variant => variant.Covers(effectiveYear));
    }

    private static IReadOnlyList<CareerTitleVariant>
        ParseCareerVariants(string text)
    {
        var lines = text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);

        const string expectedHeader =
            "CareerId,StartYear,EndYear,DisplayName,Level1Title,Level2Title,Level3Title,Level4Title,Level5Title";

        if (lines.Length < 2
            || !lines[0]
                .TrimStart('\uFEFF')
                .Equals(
                    expectedHeader,
                    StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                CareerVariantsPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                expectedHeader);
        }

        var result = new List<CareerTitleVariant>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 9)
            {
                throw CatalogValidation.FieldCount(
                    CareerVariantsPath,
                    row,
                    fields.Length,
                    9);
            }

            result.Add(
                new CareerTitleVariant(
                    row,
                    fields[0].Trim(),
                    CatalogValidation.ParseInt(CareerVariantsPath, row, "StartYear", fields[1]),
                    string.IsNullOrWhiteSpace(fields[2])
                        ? null
                        : CatalogValidation.ParseInt(CareerVariantsPath, row, "EndYear", fields[2]),
                    Optional(fields[3]),
                    Optional(fields[4]),
                    Optional(fields[5]),
                    Optional(fields[6]),
                    Optional(fields[7]),
                    Optional(fields[8])));
        }

        return result;
    }

    private static IReadOnlyList<PersonStatusVariant>
        ParseStatusVariants(string text)
    {
        var lines = text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);

        const string expectedHeader =
            "StatusId,StartYear,EndYear,Label";

        if (lines.Length < 2
            || !lines[0]
                .TrimStart('\uFEFF')
                .Equals(
                    expectedHeader,
                    StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                StatusVariantsPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                expectedHeader);
        }

        var result = new List<PersonStatusVariant>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 4)
            {
                throw CatalogValidation.FieldCount(
                    StatusVariantsPath,
                    row,
                    fields.Length,
                    4);
            }

            result.Add(
                new PersonStatusVariant(
                    row,
                    fields[0].Trim(),
                    CatalogValidation.ParseInt(StatusVariantsPath, row, "StartYear", fields[1]),
                    string.IsNullOrWhiteSpace(fields[2])
                        ? null
                        : CatalogValidation.ParseInt(StatusVariantsPath, row, "EndYear", fields[2]),
                    fields[3].Trim()));
        }

        return result;
    }

    private static void ValidateCareerVariants(
        IReadOnlyList<CareerTitleVariant> variants,
        IReadOnlySet<string> knownCareerIds)
    {
        foreach (var variant in variants)
        {
            if (!knownCareerIds.Contains(variant.CareerId))
            {
                throw CatalogValidation.Error(
                    CareerVariantsPath,
                    "a CareerId defined in Career/careers.csv",
                    variant.SourceRow,
                    variant.CareerId,
                    "CareerId",
                    variant.CareerId);
            }

            ValidateRange(
                variant.CareerId,
                variant.StartYear,
                variant.EndYear,
                variant.SourceRow,
                CareerVariantsPath);

            if (variant.AllOverridesBlank)
            {
                throw CatalogValidation.Error(
                    CareerVariantsPath,
                    "at least one non-empty presentation override",
                    variant.SourceRow,
                    variant.CareerId,
                    "DisplayName/LevelTitles",
                    "<all blank>");
            }
        }

        ValidateNoOverlaps(
            variants.Select(
                variant =>
                    (variant.CareerId, variant.StartYear, variant.EndYear, variant.SourceRow)),
            CareerVariantsPath);
    }

    private static void ValidateStatusVariants(
        IReadOnlyList<PersonStatusVariant> variants)
    {
        foreach (var variant in variants)
        {
            if (!KnownStatusIds.Contains(variant.StatusId))
            {
                throw CatalogValidation.Error(
                    StatusVariantsPath,
                    $"one of: {string.Join(", ", KnownStatusIds.OrderBy(value => value))}",
                    variant.SourceRow,
                    variant.StatusId,
                    "StatusId",
                    variant.StatusId);
            }

            if (string.IsNullOrWhiteSpace(variant.Label))
            {
                throw CatalogValidation.Error(
                    StatusVariantsPath,
                    "a non-empty label",
                    variant.SourceRow,
                    variant.StatusId,
                    "Label",
                    variant.Label);
            }

            ValidateRange(
                variant.StatusId,
                variant.StartYear,
                variant.EndYear,
                variant.SourceRow,
                StatusVariantsPath);
        }

        ValidateNoOverlaps(
            variants.Select(
                variant =>
                    (variant.StatusId, variant.StartYear, variant.EndYear, variant.SourceRow)),
            StatusVariantsPath);
    }

    private static void ValidateRange(
        string id,
        int startYear,
        int? endYear,
        int sourceRow,
        string path)
    {
        if (startYear < GameCalendarConfiguration.GameStartYear)
        {
            throw CatalogValidation.Error(
                path,
                $"a year at or after {GameCalendarConfiguration.GameStartYear}",
                sourceRow,
                id,
                "StartYear",
                startYear);
        }

        if (endYear is int end
            && end < startYear)
        {
            throw CatalogValidation.Error(
                path,
                $"a year at or after StartYear ({startYear})",
                sourceRow,
                id,
                "EndYear",
                end);
        }
    }

    private static void ValidateNoOverlaps(
        IEnumerable<(string Id, int StartYear, int? EndYear, int SourceRow)> rows,
        string path)
    {
        foreach (var group in rows.GroupBy(
            row => row.Id,
            StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group
                .OrderBy(row => row.StartYear)
                .ToList();

            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];

                if (previous.EndYear is null
                    || previous.EndYear.Value >= current.StartYear)
                {
                    throw CatalogValidation.Error(
                        path,
                        $"a StartYear after the previous range from row {previous.SourceRow}",
                        current.SourceRow,
                        group.Key,
                        "StartYear",
                        current.StartYear);
                }
            }
        }
    }

    private static string? Optional(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0
            ? null
            : trimmed;
    }

    private sealed record CareerTitleVariant(
        int SourceRow,
        string CareerId,
        int StartYear,
        int? EndYear,
        string? DisplayName,
        string? Level1Title,
        string? Level2Title,
        string? Level3Title,
        string? Level4Title,
        string? Level5Title)
    {
        public bool Covers(int year) =>
            year >= StartYear
            && (EndYear is null || year <= EndYear.Value);

        public bool AllOverridesBlank =>
            DisplayName is null
            && Level1Title is null
            && Level2Title is null
            && Level3Title is null
            && Level4Title is null
            && Level5Title is null;
    }

    private sealed record PersonStatusVariant(
        int SourceRow,
        string StatusId,
        int StartYear,
        int? EndYear,
        string Label)
    {
        public bool Covers(int year) =>
            year >= StartYear
            && (EndYear is null || year <= EndYear.Value);
    }
}
