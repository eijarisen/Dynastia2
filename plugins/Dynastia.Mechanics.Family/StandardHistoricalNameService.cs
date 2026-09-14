using System.Globalization;
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

            if (parts.Length != 4
                || !int.TryParse(
                    parts[0].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var startYear))
            {
                throw new InvalidDataException(
                    $"{EraPath}:{lineNumber}: invalid name-era row.");
            }

            int? endYear = null;
            var endText = parts[1].Trim();

            if (endText.Length > 0)
            {
                if (!int.TryParse(
                        endText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsedEnd))
                {
                    throw new InvalidDataException(
                        $"{EraPath}:{lineNumber}: invalid end year.");
                }

                endYear = parsedEnd;
            }

            var maleFile = parts[2].Trim();
            var femaleFile = parts[3].Trim();

            if (maleFile.Length == 0
                || femaleFile.Length == 0)
            {
                throw new InvalidDataException(
                    $"{EraPath}:{lineNumber}: both male and female catalogues are required.");
            }

            rows.Add(
                new NameEra(
                    startYear,
                    endYear,
                    maleFile,
                    femaleFile));
        }

        if (rows.Count == 0)
        {
            throw new InvalidDataException(
                $"{EraPath}: no name eras were defined.");
        }

        return rows;
    }

    private static void ValidateCoverage(
        IReadOnlyList<NameEra> eras)
    {
        if (eras[0].StartYear != 1700)
        {
            throw new InvalidDataException(
                $"{EraPath}: coverage must begin in 1700.");
        }

        for (var index = 0;
            index < eras.Count;
            index++)
        {
            var era = eras[index];

            if (era.EndYear is int endYear
                && endYear < era.StartYear)
            {
                throw new InvalidDataException(
                    $"{EraPath}: era beginning {era.StartYear} ends before it starts.");
            }

            var isLast =
                index == eras.Count - 1;

            if (isLast)
            {
                if (era.EndYear is not null)
                {
                    throw new InvalidDataException(
                        $"{EraPath}: final era must be open-ended.");
                }

                continue;
            }

            if (era.EndYear is null)
            {
                throw new InvalidDataException(
                    $"{EraPath}: only the final era may be open-ended.");
            }

            var next = eras[index + 1];

            if (next.StartYear
                != era.EndYear.Value + 1)
            {
                throw new InvalidDataException(
                    $"{EraPath}: gap or overlap between {era.StartYear} and {next.StartYear}.");
            }
        }
    }

    private sealed record NameEra(
        int StartYear,
        int? EndYear,
        string MaleFile,
        string FemaleFile);
}
