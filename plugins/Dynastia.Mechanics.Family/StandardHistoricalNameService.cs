using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class StandardHistoricalNameService : IHistoricalNameService
{
    private const string EraPath =
        "Names/name_eras.csv";

    private const string NameCulturesPath =
        "Names/name_cultures.json";

    private const string ModernMalePath =
        "Names/polish_male.csv";

    private const string ModernFemalePath =
        "Names/polish_female.csv";

    private readonly IReadOnlyList<NameEra> _eras;
    private readonly IReadOnlyDictionary<string, NameCultureDefinition> _cultures;
    private readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<WeightedStringEntry>> _catalogues;

    private StandardHistoricalNameService(
        IReadOnlyList<NameEra> eras,
        IReadOnlyDictionary<string, NameCultureDefinition> cultures,
        IReadOnlyDictionary<
            string,
            IReadOnlyList<WeightedStringEntry>> catalogues)
    {
        _eras = eras;
        _cultures = cultures;
        _catalogues = catalogues;
    }

    public static StandardHistoricalNameService Load(
        IGameDataService data,
        bool loadNationalityCultures = true)
    {
        ArgumentNullException.ThrowIfNull(data);

        var eras = ParseEras(
            data.ReadText(EraPath));

        ValidateCoverage(eras);

        var cultures = loadNationalityCultures
            ? ParseNameCultures(
                data.ReadText(NameCulturesPath))
            : CreatePolishOnlyCulture();

        var paths = eras
            .SelectMany(era =>
                new[]
                {
                    era.MaleFile,
                    era.FemaleFile
                })
            .Append(ModernMalePath)
            .Append(ModernFemalePath)
            .Concat(
                cultures.Values.SelectMany(
                    culture =>
                        new[]
                        {
                            culture.MaleFile,
                            culture.FemaleFile,
                            culture.SurnameFile
                        }))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
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

        if (loadNationalityCultures)
        {
            ValidateNameCultures(
                cultures,
                catalogues);
        }

        return new StandardHistoricalNameService(
            eras,
            cultures,
            catalogues);
    }

    public string GetRandomFirstName(
        Sex sex,
        int birthYear,
        IGameRandom random) =>
        GetRandomFirstName(
            sex,
            birthYear,
            "polish",
            random);

    public string GetRandomFirstName(
        Sex sex,
        int birthYear,
        string nameCultureId,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return SelectWeighted(
            ResolveFirstNameCatalogue(
                sex,
                birthYear,
                nameCultureId),
            random);
    }

    public string GetRandomDifferentFirstName(
        Sex sex,
        int birthYear,
        string excludedName,
        IGameRandom random)
    {
        return GetRandomDifferentFirstName(
            sex,
            birthYear,
            excludedName,
            "polish",
            random);
    }

    public string GetRandomDifferentFirstName(
        Sex sex,
        int birthYear,
        string excludedName,
        string nameCultureId,
        IGameRandom random)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            excludedName);

        return GetRandomFirstNameExcluding(
            sex,
            birthYear,
            [excludedName],
            nameCultureId,
            random);
    }

    public string GetRandomFirstNameExcluding(
        Sex sex,
        int birthYear,
        IReadOnlyCollection<string> excludedNames,
        IGameRandom random)
    {
        return GetRandomFirstNameExcluding(
            sex,
            birthYear,
            excludedNames,
            "polish",
            random);
    }

    public string GetRandomFirstNameExcluding(
        Sex sex,
        int birthYear,
        IReadOnlyCollection<string> excludedNames,
        string nameCultureId,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(excludedNames);
        ArgumentNullException.ThrowIfNull(random);

        if (excludedNames.Count == 0)
        {
            return GetRandomFirstName(
                sex,
                birthYear,
                nameCultureId,
                random);
        }

        var excluded =
            excludedNames.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        var candidates = ResolveFirstNameCatalogue(
                sex,
                birthYear,
                nameCultureId)
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

    public string GetRandomSurname(
        Sex sex,
        string nameCultureId,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var culture =
            GetCulture(nameCultureId);

        return SelectWeighted(
            GetCatalogue(
                culture.SurnameFile,
                culture.Id,
                "surname"),
            random);
    }

    public string FormatSurname(
        string surname,
        Sex sex,
        string nameCultureId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surname);

        var culture =
            GetCulture(nameCultureId);

        if (!culture.Id.Equals(
                "polish",
                StringComparison.OrdinalIgnoreCase)
            || sex != Sex.Female)
        {
            return surname;
        }

        if (surname.EndsWith(
            "ski",
            StringComparison.OrdinalIgnoreCase))
        {
            return surname[..^3] + "ska";
        }

        if (surname.EndsWith(
            "cki",
            StringComparison.OrdinalIgnoreCase))
        {
            return surname[..^3] + "cka";
        }

        return surname;
    }

    public bool HasNameCulture(
        string nameCultureId) =>
        !string.IsNullOrWhiteSpace(nameCultureId)
        && _cultures.ContainsKey(nameCultureId);

    private IReadOnlyList<WeightedStringEntry>
        ResolveFirstNameCatalogue(
            Sex sex,
            int birthYear,
            string nameCultureId)
    {
        var culture =
            GetCulture(nameCultureId);

        if (culture.Mode.Equals(
                "historical_era_first_names",
                StringComparison.OrdinalIgnoreCase))
        {
            return ResolvePolishHistoricalCatalogue(
                sex,
                birthYear);
        }

        if (!culture.Mode.Equals(
                "base_weighted",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Name culture '{culture.Id}' has unsupported mode '{culture.Mode}'.");
        }

        var path = sex == Sex.Male
            ? culture.MaleFile
            : culture.FemaleFile;

        return GetCatalogue(
            path,
            culture.Id,
            sex == Sex.Male
                ? "male first-name"
                : "female first-name");
    }

    private IReadOnlyList<WeightedStringEntry>
        ResolvePolishHistoricalCatalogue(
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

        // Normal startup validation guarantees the historical catalogues
        // exist. Keep the legacy modern fallback only for Polish data.
        var fallback = sex == Sex.Male
            ? ModernMalePath
            : ModernFemalePath;

        return _catalogues[fallback];
    }

    private IReadOnlyList<WeightedStringEntry>
        GetCatalogue(
            string? path,
            string cultureId,
            string kind)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !_catalogues.TryGetValue(
                path,
                out var catalogue))
        {
            throw new InvalidOperationException(
                $"Name culture '{cultureId}' has no loaded {kind} catalogue.");
        }

        return catalogue;
    }

    private NameCultureDefinition GetCulture(
        string nameCultureId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            nameCultureId);

        if (_cultures.TryGetValue(
                nameCultureId,
                out var culture))
        {
            return culture;
        }

        throw new InvalidOperationException(
            $"Unknown name culture '{nameCultureId}'.");
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

    private static IReadOnlyDictionary<string, NameCultureDefinition>
        ParseNameCultures(
            string text)
    {
        var rows = JsonSerializer.Deserialize<List<NameCultureDefinition>>(
                text,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? throw CatalogValidation.Error(
                NameCulturesPath,
                "a JSON array of name cultures");

        if (rows.Count != 25)
        {
            throw CatalogValidation.Error(
                NameCulturesPath,
                "exactly 25 name cultures",
                field: "Count",
                value: rows.Count);
        }

        var result =
            new Dictionary<string, NameCultureDefinition>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Id))
            {
                throw CatalogValidation.Error(
                    NameCulturesPath,
                    "a non-empty culture id",
                    field: "id",
                    value: row.Id);
            }

            if (!result.TryAdd(row.Id, row))
            {
                throw CatalogValidation.Error(
                    NameCulturesPath,
                    "unique culture ids",
                    field: "id",
                    value: row.Id);
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, NameCultureDefinition>
        CreatePolishOnlyCulture()
    {
        return new Dictionary<string, NameCultureDefinition>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["polish"] = new NameCultureDefinition
            {
                Id = "polish",
                DisplayName = "Polish",
                Mode = "historical_era_first_names",
                EraFile = EraPath,
                SurnameFile = string.Empty
            }
        };
    }

    private static void ValidateNameCultures(
        IReadOnlyDictionary<string, NameCultureDefinition> cultures,
        IReadOnlyDictionary<string, IReadOnlyList<WeightedStringEntry>> catalogues)
    {
        if (!cultures.TryGetValue(
                "polish",
                out var polish)
            || !polish.Mode.Equals(
                "historical_era_first_names",
                StringComparison.OrdinalIgnoreCase))
        {
            throw CatalogValidation.Error(
                NameCulturesPath,
                "a Polish historical-era name culture",
                field: "polish.mode",
                value: polish?.Mode);
        }

        foreach (var culture in cultures.Values)
        {
            if (string.IsNullOrWhiteSpace(culture.SurnameFile)
                || !catalogues.TryGetValue(
                    culture.SurnameFile,
                    out var surnames))
            {
                throw CatalogValidation.Error(
                    NameCulturesPath,
                    "a loaded surname catalogue for every culture",
                    field: $"{culture.Id}.surnameFile",
                    value: culture.SurnameFile);
            }

            if (culture.Id.Equals(
                    "polish",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!culture.Mode.Equals(
                    "base_weighted",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw CatalogValidation.Error(
                    NameCulturesPath,
                    "base_weighted for every non-Polish culture",
                    field: $"{culture.Id}.mode",
                    value: culture.Mode);
            }

            ValidateExternalCount(
                culture,
                catalogues,
                culture.MaleFile,
                culture.MaleCount,
                100,
                "male");

            ValidateExternalCount(
                culture,
                catalogues,
                culture.FemaleFile,
                culture.FemaleCount,
                100,
                "female");

            if (culture.SurnameCount < 500
                || surnames.Count != culture.SurnameCount)
            {
                throw CatalogValidation.Error(
                    NameCulturesPath,
                    "at least 500 surname entries with surnameCount matching the loaded catalogue",
                    field: $"{culture.Id}.surnameCount",
                    value: $"{culture.SurnameCount} declared / {surnames.Count} loaded");
            }
        }
    }

    private static void ValidateExternalCount(
        NameCultureDefinition culture,
        IReadOnlyDictionary<string, IReadOnlyList<WeightedStringEntry>> catalogues,
        string? path,
        int declaredCount,
        int minimumCount,
        string sexLabel)
    {
        IReadOnlyList<WeightedStringEntry>? entries = null;
        var hasCatalogue =
            !string.IsNullOrWhiteSpace(path)
            && catalogues.TryGetValue(path, out entries);

        if (!hasCatalogue
            || declaredCount < minimumCount
            || entries!.Count != declaredCount)
        {
            throw CatalogValidation.Error(
                NameCulturesPath,
                $"at least {minimumCount} {sexLabel} first-name entries with {sexLabel}Count matching the loaded catalogue",
                field: $"{culture.Id}.{sexLabel}Count",
                value: $"{declaredCount} declared / {entries?.Count ?? 0} loaded");
        }
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

    private sealed class NameCultureDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string? EraFile { get; set; }
        public string? MaleFile { get; set; }
        public string? FemaleFile { get; set; }
        public string SurnameFile { get; set; } = string.Empty;
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int SurnameCount { get; set; }
    }
}
