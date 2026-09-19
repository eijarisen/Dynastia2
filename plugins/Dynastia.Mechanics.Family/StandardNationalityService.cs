using System.Globalization;
using System.Text;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class StandardNationalityService : INationalityService
{
    private const string NationalitiesPath =
        "Nationalities/nationalities.csv";

    private const string RegionalWeightsPath =
        "Nationalities/regional_nationality_weights.csv";

    private const string GenerationRulesPath =
        "Nationalities/nationality_generation_rules.json";

    private const string TownCatalogPath =
        "Towns/dynastia-towns.json";

    private const double SnapshotTolerance =
        0.01;

    // Town catalogue v1.3 introduces upper_dvina as a separate economic
    // region, while the nationality data set still has the earlier 27-region
    // demographic model. Until a dedicated demographic series is supplied,
    // use the adjacent eastern_belarus profile rather than inventing weights.
    private static readonly IReadOnlyDictionary<string, string>
        RegionDistributionFallbacks =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["upper_dvina"] = "eastern_belarus"
            };

    private readonly IHistoricalNameService _names;
    private readonly IReadOnlyList<NationalityDefinition> _orderedDefinitions;
    private readonly IReadOnlyDictionary<string, NationalityDefinition> _definitions;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<RegionalSnapshot>> _snapshots;
    private readonly string _defaultNationalityId;
    private readonly int _minimumYear;
    private readonly int _maximumYear;

    private StandardNationalityService(
        IHistoricalNameService names,
        IReadOnlyList<NationalityDefinition> orderedDefinitions,
        IReadOnlyDictionary<string, NationalityDefinition> definitions,
        IReadOnlyDictionary<string, IReadOnlyList<RegionalSnapshot>> snapshots,
        string defaultNationalityId,
        int minimumYear,
        int maximumYear)
    {
        _names = names;
        _orderedDefinitions = orderedDefinitions;
        _definitions = definitions;
        _snapshots = snapshots;
        _defaultNationalityId = defaultNationalityId;
        _minimumYear = minimumYear;
        _maximumYear = maximumYear;
    }

    public static StandardNationalityService Load(
        IGameDataService data,
        IHistoricalNameService names)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(names);

        var definitions =
            ParseNationalities(
                data.ReadText(NationalitiesPath));

        if (definitions.Count != 25)
        {
            throw CatalogValidation.Error(
                NationalitiesPath,
                "exactly 25 nationality definitions",
                field: "Count",
                value: definitions.Count);
        }

        var byId =
            definitions.ToDictionary(
                item => item.Id,
                StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (definition.GenerationEnabled
                && !names.HasNameCulture(
                    definition.NameCultureId))
            {
                throw CatalogValidation.Error(
                    NationalitiesPath,
                    "a NameCultureId defined in Names/name_cultures.json",
                    item: definition.Id,
                    field: "NameCultureId",
                    value: definition.NameCultureId);
            }
        }

        var rules = ParseGenerationRules(
            data.ReadText(GenerationRulesPath));

        if (!byId.ContainsKey(
                rules.DefaultNationalityId))
        {
            throw CatalogValidation.Error(
                GenerationRulesPath,
                "a registered default nationality",
                field: "defaultNationalityId",
                value: rules.DefaultNationalityId);
        }

        if (rules.MechanicalEffectsEnabled)
        {
            throw CatalogValidation.Error(
                GenerationRulesPath,
                "mechanicalEffectsEnabled=false for the identity-only nationality system",
                field: "mechanicalEffectsEnabled",
                value: true);
        }

        var townRegionIds =
            ParseTownRegionIds(
                data.ReadText(TownCatalogPath));

        var snapshots = ParseRegionalWeights(
            data.ReadText(RegionalWeightsPath),
            byId,
            townRegionIds);

        foreach (var regionId in townRegionIds)
        {
            if (snapshots.ContainsKey(regionId))
                continue;

            if (RegionDistributionFallbacks.TryGetValue(
                    regionId,
                    out var fallbackRegionId)
                && townRegionIds.Contains(fallbackRegionId)
                && snapshots.ContainsKey(fallbackRegionId))
            {
                continue;
            }

            throw CatalogValidation.Error(
                RegionalWeightsPath,
                "at least one nationality snapshot (or a configured fallback) for every Town RegionId",
                item: regionId,
                field: "RegionId",
                value: regionId);
        }

        return new StandardNationalityService(
            names,
            definitions,
            byId,
            snapshots,
            rules.DefaultNationalityId,
            rules.MinimumYear,
            rules.MaximumYear);
    }

    public string GetNationality(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var component =
            person.Components.Get<NationalityComponent>();

        if (component is null
            || string.IsNullOrWhiteSpace(
                component.NationalityId))
        {
            throw new InvalidOperationException(
                $"Nationality state is missing for person '{person.Id}'.");
        }

        if (!_definitions.ContainsKey(
                component.NationalityId))
        {
            throw new InvalidOperationException(
                $"Unknown nationality '{component.NationalityId}' on person '{person.Id}'.");
        }

        return component.NationalityId;
    }

    public void SetNationality(
        IPerson person,
        string id)
    {
        ArgumentNullException.ThrowIfNull(person);
        var definition = GetDefinition(id);

        person.Components.Set(
            new NationalityComponent
            {
                NationalityId = definition.Id
            });
    }

    public string GetDisplayName(
        string id) =>
        GetDefinition(id).DisplayName;

    public string GetNameCultureId(
        string id) =>
        GetDefinition(id).NameCultureId;

    public IReadOnlyDictionary<string, double>
        ResolveDistribution(
            string regionId,
            int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            regionId);

        var distributionRegionId = regionId;
        if (!_snapshots.ContainsKey(distributionRegionId)
            && RegionDistributionFallbacks.TryGetValue(
                regionId,
                out var fallbackRegionId))
        {
            distributionRegionId = fallbackRegionId;
        }

        if (!_snapshots.TryGetValue(
                distributionRegionId,
                out var snapshots)
            || snapshots.Count == 0)
        {
            throw new InvalidOperationException(
                $"No nationality distribution is configured for region '{regionId}'.");
        }

        var effectiveYear = Math.Clamp(
            year,
            _minimumYear,
            _maximumYear);

        RegionalSnapshot lower;
        RegionalSnapshot upper;

        if (effectiveYear <= snapshots[0].Year)
        {
            lower = snapshots[0];
            upper = snapshots[0];
        }
        else if (effectiveYear >= snapshots[^1].Year)
        {
            lower = snapshots[^1];
            upper = snapshots[^1];
        }
        else
        {
            upper = snapshots.First(
                snapshot =>
                    snapshot.Year >= effectiveYear);

            lower = snapshots.Last(
                snapshot =>
                    snapshot.Year <= effectiveYear);
        }

        var fraction = lower.Year == upper.Year
            ? 0.0
            : (effectiveYear - lower.Year)
              / (double)(upper.Year - lower.Year);

        var raw =
            new List<KeyValuePair<string, double>>(
                _orderedDefinitions.Count);

        foreach (var definition in _orderedDefinitions)
        {
            var low = lower.Weights.TryGetValue(
                definition.Id,
                out var lowWeight)
                    ? lowWeight
                    : 0.0;

            var high = upper.Weights.TryGetValue(
                definition.Id,
                out var highWeight)
                    ? highWeight
                    : 0.0;

            var weight = low
                + (high - low) * fraction;

            raw.Add(
                new KeyValuePair<string, double>(
                    definition.Id,
                    Math.Max(0.0, weight)));
        }

        var total = raw.Sum(pair => pair.Value);
        if (total <= 0.0)
        {
            throw new InvalidOperationException(
                $"Nationality distribution for region '{regionId}' in {effectiveYear} has zero total weight.");
        }

        var normalized =
            new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var pair in raw)
        {
            normalized[pair.Key] =
                pair.Value / total * 100.0;
        }

        return normalized;
    }

    public string GenerateNationality(
        string regionId,
        int year,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var distribution =
            ResolveDistribution(
                regionId,
                year);

        var enabled = _orderedDefinitions
            .Where(definition => definition.GenerationEnabled)
            .ToList();

        var enabledTotal = enabled.Sum(
            definition =>
                distribution[definition.Id]);

        if (enabledTotal <= 0.0)
        {
            throw new InvalidOperationException(
                $"Region '{regionId}' has no enabled nationalities in {year}.");
        }

        var roll =
            random.NextDouble()
            * enabledTotal;

        foreach (var definition in enabled)
        {
            var weight =
                distribution[definition.Id];

            if (roll < weight)
                return definition.Id;

            roll -= weight;
        }

        return enabled[^1].Id;
    }

    public string FormatSurname(
        IPerson person,
        string surname,
        Sex sex)
    {
        ArgumentNullException.ThrowIfNull(person);

        var nationalityId =
            GetNationality(person);

        return _names.FormatSurname(
            surname,
            sex,
            GetNameCultureId(
                nationalityId));
    }

    internal void InitializeDefault(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (person.Components.Has<NationalityComponent>())
            return;

        SetNationality(
            person,
            _defaultNationalityId);
    }

    private NationalityDefinition GetDefinition(
        string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (_definitions.TryGetValue(
                id,
                out var definition))
        {
            return definition;
        }

        throw new InvalidOperationException(
            $"Unknown nationality '{id}'.");
    }

    private static List<NationalityDefinition> ParseNationalities(
        string text)
    {
        var lines = text.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count < 2)
        {
            throw CatalogValidation.Error(
                NationalitiesPath,
                "a header and at least one nationality row");
        }

        var header =
            string.Join(
                ",",
                ParseCsvLine(lines[0]))
            .TrimStart('\uFEFF');

        const string expectedHeader =
            "Id,DisplayName,NameCultureId,GenerationEnabled,Notes";

        if (!header.Equals(
                expectedHeader,
                StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                NationalitiesPath,
                header,
                expectedHeader);
        }

        var rows =
            new List<NationalityDefinition>();

        var ids =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (var index = 1;
            index < lines.Count;
            index++)
        {
            var fields = ParseCsvLine(lines[index]);
            var row = index + 1;

            if (fields.Count != 5)
            {
                throw CatalogValidation.FieldCount(
                    NationalitiesPath,
                    row,
                    fields.Count,
                    5);
            }

            var id = fields[0].Trim().TrimStart('\uFEFF');
            var displayName = fields[1].Trim();
            var nameCultureId = fields[2].Trim();

            if (id.Length == 0
                || displayName.Length == 0
                || nameCultureId.Length == 0)
            {
                throw CatalogValidation.Error(
                    NationalitiesPath,
                    "non-empty Id, DisplayName and NameCultureId values",
                    row,
                    field: "Identity",
                    value: lines[index]);
            }

            if (!ids.Add(id))
            {
                throw CatalogValidation.Error(
                    NationalitiesPath,
                    "unique NationalityId values",
                    row,
                    field: "Id",
                    value: id);
            }

            var enabledText = fields[3].Trim();
            var enabled = enabledText switch
            {
                "1" => true,
                "0" => false,
                _ => throw CatalogValidation.Error(
                    NationalitiesPath,
                    "0 or 1",
                    row,
                    field: "GenerationEnabled",
                    value: enabledText)
            };

            rows.Add(
                new NationalityDefinition(
                    id,
                    displayName,
                    nameCultureId,
                    enabled));
        }

        return rows;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<RegionalSnapshot>>
        ParseRegionalWeights(
            string text,
            IReadOnlyDictionary<string, NationalityDefinition> definitions,
            IReadOnlySet<string> townRegionIds)
    {
        var lines = text.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count < 2)
        {
            throw CatalogValidation.Error(
                RegionalWeightsPath,
                "a header and nationality-weight rows");
        }

        var header = lines[0]
            .TrimStart('\uFEFF')
            .Trim();

        const string expectedHeader =
            "RegionId,Year,NationalityId,WeightPercent";

        if (!header.Equals(
                expectedHeader,
                StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                RegionalWeightsPath,
                header,
                expectedHeader);
        }

        var grouped =
            new Dictionary<
                string,
                Dictionary<int, Dictionary<string, double>>>(
                    StringComparer.OrdinalIgnoreCase);

        for (var index = 1;
            index < lines.Count;
            index++)
        {
            var row = index + 1;
            var fields = lines[index].Split(',');

            if (fields.Length != 4)
            {
                throw CatalogValidation.FieldCount(
                    RegionalWeightsPath,
                    row,
                    fields.Length,
                    4);
            }

            var regionId =
                fields[0].Trim().TrimStart('\uFEFF');

            if (!townRegionIds.Contains(regionId))
            {
                throw CatalogValidation.Error(
                    RegionalWeightsPath,
                    "a RegionId defined in Towns/dynastia-towns.json",
                    row,
                    field: "RegionId",
                    value: regionId);
            }

            var year = CatalogValidation.ParseInt(
                RegionalWeightsPath,
                row,
                "Year",
                fields[1]);

            var nationalityId =
                fields[2].Trim();

            if (!definitions.ContainsKey(
                    nationalityId))
            {
                throw CatalogValidation.Error(
                    RegionalWeightsPath,
                    "a NationalityId defined in Nationalities/nationalities.csv",
                    row,
                    field: "NationalityId",
                    value: nationalityId);
            }

            var weight = CatalogValidation.ParseDouble(
                RegionalWeightsPath,
                row,
                "WeightPercent",
                fields[3]);

            if (weight < 0.0)
            {
                throw CatalogValidation.Error(
                    RegionalWeightsPath,
                    "a non-negative percentage",
                    row,
                    field: "WeightPercent",
                    value: weight);
            }

            if (!grouped.TryGetValue(
                    regionId,
                    out var byYear))
            {
                byYear = [];
                grouped[regionId] = byYear;
            }

            if (!byYear.TryGetValue(
                    year,
                    out var weights))
            {
                weights = new Dictionary<string, double>(
                    StringComparer.OrdinalIgnoreCase);
                byYear[year] = weights;
            }

            if (!weights.TryAdd(
                    nationalityId,
                    weight))
            {
                throw CatalogValidation.Error(
                    RegionalWeightsPath,
                    "one row per RegionId/Year/NationalityId",
                    row,
                    field: "NationalityId",
                    value: nationalityId);
            }
        }

        var result =
            new Dictionary<string, IReadOnlyList<RegionalSnapshot>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var region in grouped)
        {
            var snapshots =
                new List<RegionalSnapshot>();

            foreach (var year in region.Value.OrderBy(pair => pair.Key))
            {
                var total = year.Value.Values.Sum();

                if (Math.Abs(total - 100.0)
                    > SnapshotTolerance)
                {
                    throw CatalogValidation.Error(
                        RegionalWeightsPath,
                        $"a total weight of 100 ± {SnapshotTolerance.ToString(CultureInfo.InvariantCulture)}",
                        item: $"{region.Key}/{year.Key}",
                        field: "WeightPercentSum",
                        value: total);
                }

                snapshots.Add(
                    new RegionalSnapshot(
                        year.Key,
                        year.Value));
            }

            result[region.Key] = snapshots;
        }

        return result;
    }

    private static HashSet<string> ParseTownRegionIds(
        string text)
    {
        using var document =
            JsonDocument.Parse(text);

        if (!document.RootElement.TryGetProperty(
                "regions",
                out var regions)
            || regions.ValueKind != JsonValueKind.Array)
        {
            throw CatalogValidation.Error(
                TownCatalogPath,
                "a regions JSON array",
                field: "regions",
                value: null);
        }

        var ids =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var region in regions.EnumerateArray())
        {
            var id = region.TryGetProperty(
                    "id",
                    out var idElement)
                ? idElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(id)
                || !ids.Add(id))
            {
                throw CatalogValidation.Error(
                    TownCatalogPath,
                    "unique non-empty region ids",
                    field: "regions[].id",
                    value: id);
            }
        }

        if (ids.Count == 0)
        {
            throw CatalogValidation.Error(
                TownCatalogPath,
                "at least one region",
                field: "regions.Count",
                value: ids.Count);
        }

        return ids;
    }

    private static GenerationRules ParseGenerationRules(
        string text)
    {
        using var document =
            JsonDocument.Parse(text);

        var root = document.RootElement;

        var defaultId = root
            .GetProperty("defaultNationalityId")
            .GetString();

        var mechanicalEffects = root
            .GetProperty("mechanicalEffectsEnabled")
            .GetBoolean();

        var years = root
            .GetProperty("supportedYears");

        var minYear = years
            .GetProperty("min")
            .GetInt32();

        var maxYear = years
            .GetProperty("max")
            .GetInt32();

        if (string.IsNullOrWhiteSpace(defaultId)
            || minYear > maxYear)
        {
            throw CatalogValidation.Error(
                GenerationRulesPath,
                "a valid default nationality and supported year range");
        }

        return new GenerationRules(
            defaultId,
            minYear,
            maxYear,
            mechanicalEffects);
    }

    private static IReadOnlyList<string> ParseCsvLine(
        string line)
    {
        var result = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0;
            index < line.Length;
            index++)
        {
            var character = line[index];

            if (character == '"')
            {
                if (quoted
                    && index + 1 < line.Length
                    && line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                    continue;
                }

                quoted = !quoted;
                continue;
            }

            if (character == ',' && !quoted)
            {
                result.Add(field.ToString());
                field.Clear();
                continue;
            }

            field.Append(character);
        }

        if (quoted)
        {
            throw CatalogValidation.Error(
                NationalitiesPath,
                "balanced CSV quotes",
                field: "Row",
                value: line);
        }

        result.Add(field.ToString());
        return result;
    }

    private sealed record NationalityDefinition(
        string Id,
        string DisplayName,
        string NameCultureId,
        bool GenerationEnabled);

    private sealed record RegionalSnapshot(
        int Year,
        IReadOnlyDictionary<string, double> Weights);

    private sealed record GenerationRules(
        string DefaultNationalityId,
        int MinimumYear,
        int MaximumYear,
        bool MechanicalEffectsEnabled);
}
