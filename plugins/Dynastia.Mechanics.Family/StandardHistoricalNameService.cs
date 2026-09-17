using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class StandardHistoricalNameService : IHistoricalNameService
{
    private const string EraPath =
        "Names/name_eras.csv";

    private const string ModernMalePath =
        "Names/polish_male.csv";

    private const string ModernFemalePath =
        "Names/polish_female.csv";

    private readonly IReadOnlyList<NameEra> _eras;
    private readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<WeightedStringEntry>> _catalogues;

    private StandardHistoricalNameService(
        IReadOnlyList<NameEra> eras,
        IReadOnlyDictionary<
            string,
            IReadOnlyList<WeightedStringEntry>> catalogues)
    {
        _eras = eras;
        _catalogues = catalogues;
    }

    public static StandardHistoricalNameService Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var eras = ParseEras(
            data.ReadText(EraPath));

        ValidateCoverage(eras);

        var paths = eras
            .SelectMany(era =>
                new[]
                {
                    era.MaleFile,
                    era.FemaleFile
                })
            .Append(ModernMalePath)
            .Append(ModernFemalePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var catalogues =
            new Dictionary<
                string,
                IReadOnlyList<WeightedStringEntry>>(
                    StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            // IGameDataService validates existence, non-empty content,
            // unique names and strictly positive weights.
            catalogues[path] =
                data.GetWeightedStringList(path);
        }

        return new StandardHistoricalNameService(
            eras,
            catalogues);
    }

    public string GetRandomFirstName(
        Sex sex,
        int birthYear,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return SelectWeighted(
            ResolveCatalogue(sex, birthYear),
            random);
    }

    public string GetRandomDifferentFirstName(
        Sex sex,
        int birthYear,
        string excludedName,
        IGameRandom random)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            excludedName);

        return GetRandomFirstNameExcluding(
            sex,
            birthYear,
            [excludedName],
            random);
    }

    public string GetRandomFirstNameExcluding(
        Sex sex,
        int birthYear,
        IReadOnlyCollection<string> excludedNames,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(excludedNames);
        ArgumentNullException.ThrowIfNull(random);

        if (excludedNames.Count == 0)
        {
            return GetRandomFirstName(
                sex,
                birthYear,
                random);
        }

        var excluded =
            excludedNames.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        var candidates = ResolveCatalogue(
                sex,
                birthYear)
            .Where(entry =>
                !excluded.Contains(entry.Value))
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                "No unused first names remain in the historical name catalogue.");
        }

        return SelectWeighted(
            candidates,
            random);
    }

    private IReadOnlyList<WeightedStringEntry>
        ResolveCatalogue(
            Sex sex,
            int birthYear)
    {
        var era = birthYear < _eras[0].StartYear
            ? _eras[0]
            : _eras.LastOrDefault(candidate =>
                birthYear >= candidate.StartYear
                && (candidate.EndYear is null
                    || birthYear <= candidate.EndYear.Value))
                ?? _eras[^1];

        var path = sex == Sex.Male
            ? era.MaleFile
            : era.FemaleFile;

        if (_catalogues.TryGetValue(
                path,
                out var catalogue))
        {
            return catalogue;
        }

        // Defensive fallback. Normal startup validation guarantees the
        // historical catalogues exist, but a modern pool is still kept as
        // the last-resort source requested by the historical-name design.
        var fallback = sex == Sex.Male
            ? ModernMalePath
            : ModernFemalePath;

        return _catalogues[fallback];
    }

    private static string SelectWeighted(
        IReadOnlyList<WeightedStringEntry> entries,
        IGameRandom random)
    {
        var totalWeight = entries.Sum(
            entry => (double)entry.Weight);

        var roll = random.NextDouble()
            * totalWeight;

        foreach (var entry in entries)
        {
            if (roll < entry.Weight)
                return entry.Value;

            roll -= entry.Weight;
        }

        return entries[^1].Value;
    }

    private static List<NameEra> ParseEras(
        string text)
    {
        var rows = new List<NameEra>();
        var lineNumber = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            lineNumber++;

            var line = rawLine.Trim();

            if (line.Length == 0
                || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith(
                    "StartYear,",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length != 4)
            {
                throw CatalogValidation.FieldCount(
                    EraPath,
                    lineNumber,
                    parts.Length,
                    4);
            }

            var startYear = CatalogValidation.ParseInt(
                EraPath,
                lineNumber,
                "StartYear",
                parts[0]);

            int? endYear = null;
            var endText = parts[1].Trim();

            if (endText.Length > 0)
            {
                endYear = CatalogValidation.ParseInt(
                    EraPath,
                    lineNumber,
                    "EndYear",
                    endText);
            }

            var maleFile = parts[2].Trim();
            var femaleFile = parts[3].Trim();

            if (maleFile.Length == 0)
            {
                throw CatalogValidation.Error(
                    EraPath,
                    "a non-empty male catalogue path",
                    lineNumber,
                    field: "MaleFile",
                    value: maleFile);
            }

            if (femaleFile.Length == 0)
            {
                throw CatalogValidation.Error(
                    EraPath,
                    "a non-empty female catalogue path",
                    lineNumber,
                    field: "FemaleFile",
                    value: femaleFile);
            }

            rows.Add(
                new NameEra(
                    lineNumber,
                    startYear,
                    endYear,
                    maleFile,
                    femaleFile));
        }

        if (rows.Count == 0)
        {
            throw CatalogValidation.Error(
                EraPath,
                "at least one name era",
                field: "Rows",
                value: 0);
        }

        return rows;
    }

    private static void ValidateCoverage(
        IReadOnlyList<NameEra> eras)
    {
        if (eras[0].StartYear != GameCalendarConfiguration.GameStartYear)
        {
            throw CatalogValidation.Error(
                EraPath,
                $"{GameCalendarConfiguration.GameStartYear} for the first era",
                eras[0].SourceRow,
                field: "StartYear",
                value: eras[0].StartYear);
        }

        for (var index = 0;
            index < eras.Count;
            index++)
        {
            var era = eras[index];

            if (era.EndYear is int endYear
                && endYear < era.StartYear)
            {
                throw CatalogValidation.Error(
                    EraPath,
                    $"a year at or after StartYear ({era.StartYear})",
                    era.SourceRow,
                    field: "EndYear",
                    value: endYear);
            }

            var isLast =
                index == eras.Count - 1;

            if (isLast)
            {
                if (era.EndYear is not null)
                {
                    throw CatalogValidation.Error(
                        EraPath,
                        "an empty EndYear for the final open-ended era",
                        era.SourceRow,
                        field: "EndYear",
                        value: era.EndYear.Value);
                }

                continue;
            }

            if (era.EndYear is null)
            {
                throw CatalogValidation.Error(
                    EraPath,
                    "an EndYear on every non-final era",
                    era.SourceRow,
                    field: "EndYear",
                    value: null);
            }

            var next = eras[index + 1];

            if (next.StartYear
                != era.EndYear.Value + 1)
            {
                throw CatalogValidation.Error(
                    EraPath,
                    $"{era.EndYear.Value + 1} so era coverage is contiguous",
                    next.SourceRow,
                    field: "StartYear",
                    value: next.StartYear);
            }
        }
    }

    private sealed record NameEra(
        int SourceRow,
        int StartYear,
        int? EndYear,
        string MaleFile,
        string FemaleFile);

}
