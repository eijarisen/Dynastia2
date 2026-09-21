using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Farming;

internal sealed class FarmingFlavorCatalog
{
    private const string FarmTypesPath = "Farming/farm_types.csv";
    private const string FarmTimePath = "Farming/farm_type_time_weights.csv";
    private const string FarmRegionPath = "Farming/farm_type_region_weights.csv";
    private const string LivestockTypesPath = "Farming/livestock_types.csv";
    private const string LivestockTimePath = "Farming/livestock_time_weights.csv";
    private const string LivestockRegionPath = "Farming/livestock_region_weights.csv";

    private readonly IReadOnlyList<FlavorDefinition> _farmTypes;
    private readonly IReadOnlyList<FlavorDefinition> _livestockTypes;
    private readonly IReadOnlyList<TimeWeight> _farmTimeWeights;
    private readonly IReadOnlyList<TimeWeight> _livestockTimeWeights;
    private readonly IReadOnlyDictionary<(string TypeId, string RegionId), decimal> _farmRegionWeights;
    private readonly IReadOnlyDictionary<(string TypeId, string RegionId), decimal> _livestockRegionWeights;

    private FarmingFlavorCatalog(
        IReadOnlyList<FlavorDefinition> farmTypes,
        IReadOnlyList<FlavorDefinition> livestockTypes,
        IReadOnlyList<TimeWeight> farmTimeWeights,
        IReadOnlyList<TimeWeight> livestockTimeWeights,
        IReadOnlyDictionary<(string TypeId, string RegionId), decimal> farmRegionWeights,
        IReadOnlyDictionary<(string TypeId, string RegionId), decimal> livestockRegionWeights)
    {
        _farmTypes = farmTypes;
        _livestockTypes = livestockTypes;
        _farmTimeWeights = farmTimeWeights;
        _livestockTimeWeights = livestockTimeWeights;
        _farmRegionWeights = farmRegionWeights;
        _livestockRegionWeights = livestockRegionWeights;
    }

    public static FarmingFlavorCatalog Load(IGameDataService data) =>
        new(
            ReadTypes(data, FarmTypesPath, "FarmType"),
            ReadTypes(data, LivestockTypesPath, "LivestockType"),
            ReadTimeWeights(data, FarmTimePath, "FarmTypeId"),
            ReadTimeWeights(data, LivestockTimePath, "LivestockTypeId"),
            ReadRegionWeights(data, FarmRegionPath, "FarmTypeId"),
            ReadRegionWeights(data, LivestockRegionPath, "LivestockTypeId"));

    public FarmingFlavorInfo SelectFarmType(string regionId, int year, double unitRoll) =>
        ToInfo(SelectWeighted(_farmTypes, _farmTimeWeights, _farmRegionWeights, regionId, year, unitRoll));

    public FarmingFlavorInfo SelectLivestock(string regionId, int year, double unitRoll) =>
        ToInfo(SelectWeighted(_livestockTypes, _livestockTimeWeights, _livestockRegionWeights, regionId, year, unitRoll));

    public IReadOnlyList<FarmingFlavorInfo> GetAvailableLivestock(string regionId, int year) =>
        BuildWeighted(_livestockTypes, _livestockTimeWeights, _livestockRegionWeights, regionId, year)
            .Where(candidate => candidate.Weight > 0m)
            .Select(candidate => ToInfo(candidate.Definition))
            .ToList();

    public FarmingFlavorInfo? FindFarmType(string? id) =>
        Find(_farmTypes, id);

    public FarmingFlavorInfo? FindLivestock(string? id) =>
        Find(_livestockTypes, id);

    private static FarmingFlavorInfo? Find(
        IReadOnlyList<FlavorDefinition> definitions,
        string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var definition = definitions.FirstOrDefault(candidate =>
            candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return definition is null ? null : ToInfo(definition);
    }

    private static FlavorDefinition SelectWeighted(
        IReadOnlyList<FlavorDefinition> definitions,
        IReadOnlyList<TimeWeight> timeWeights,
        IReadOnlyDictionary<(string TypeId, string RegionId), decimal> regionWeights,
        string regionId,
        int year,
        double unitRoll)
    {
        var weighted = BuildWeighted(definitions, timeWeights, regionWeights, regionId, year)
            .Where(candidate => candidate.Weight > 0m)
            .ToList();

        if (weighted.Count == 0)
            throw new InvalidOperationException($"No farming flavor is available for year {year} and region '{regionId}'.");

        var total = weighted.Sum(candidate => candidate.Weight);
        var roll = (decimal)Math.Clamp(unitRoll, 0d, 0.9999999999999999d) * total;
        decimal cursor = 0m;
        foreach (var candidate in weighted)
        {
            cursor += candidate.Weight;
            if (roll < cursor)
                return candidate.Definition;
        }

        return weighted[^1].Definition;
    }

    private static IReadOnlyList<WeightedFlavor> BuildWeighted(
        IReadOnlyList<FlavorDefinition> definitions,
        IReadOnlyList<TimeWeight> timeWeights,
        IReadOnlyDictionary<(string TypeId, string RegionId), decimal> regionWeights,
        string regionId,
        int year)
    {
        var normalizedRegion = regionId?.Trim() ?? string.Empty;
        var result = new List<WeightedFlavor>();

        foreach (var definition in definitions)
        {
            if (year < definition.StartYear || year > definition.EndYear)
                continue;

            var time = timeWeights.FirstOrDefault(weight =>
                weight.TypeId.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)
                && year >= weight.StartYear
                && year <= weight.EndYear)?.Multiplier ?? 1m;

            var region = regionWeights.TryGetValue((definition.Id, normalizedRegion), out var multiplier)
                ? multiplier
                : 1m;

            result.Add(new WeightedFlavor(
                definition,
                definition.BaseWeight * time * region));
        }

        return result;
    }

    private static IReadOnlyList<FlavorDefinition> ReadTypes(
        IGameDataService data,
        string path,
        string kind)
    {
        var rows = ReadRows(data, path);
        ExpectHeader(rows, path, "Id,DisplayName,Emoji,StartYear,EndYear,BaseWeight");
        var result = new List<FlavorDefinition>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < rows.Count; i++)
        {
            var fields = rows[i].Split(',');
            if (fields.Length != 6)
                throw new InvalidOperationException($"{path} row {i + 1} must contain 6 fields.");

            var id = fields[0].Trim();
            if (id.Length == 0 || !ids.Add(id))
                throw new InvalidOperationException($"{path} row {i + 1} has an empty or duplicate {kind} id '{id}'.");

            result.Add(new FlavorDefinition(
                id,
                fields[1].Trim(),
                fields[2].Trim(),
                ParseInt(fields[3], path, i + 1, "StartYear"),
                string.IsNullOrWhiteSpace(fields[4])
                    ? int.MaxValue
                    : ParseInt(fields[4], path, i + 1, "EndYear"),
                ParsePositiveDecimal(fields[5], path, i + 1, "BaseWeight")));
        }

        return result;
    }

    private static IReadOnlyList<TimeWeight> ReadTimeWeights(
        IGameDataService data,
        string path,
        string idColumn)
    {
        var rows = ReadRows(data, path);
        ExpectHeader(rows, path, $"{idColumn},StartYear,EndYear,WeightMultiplier");
        var result = new List<TimeWeight>();

        for (var i = 1; i < rows.Count; i++)
        {
            var fields = rows[i].Split(',');
            if (fields.Length != 4)
                throw new InvalidOperationException($"{path} row {i + 1} must contain 4 fields.");

            result.Add(new TimeWeight(
                fields[0].Trim(),
                ParseInt(fields[1], path, i + 1, "StartYear"),
                ParseInt(fields[2], path, i + 1, "EndYear"),
                ParsePositiveDecimal(fields[3], path, i + 1, "WeightMultiplier")));
        }

        return result;
    }

    private static IReadOnlyDictionary<(string TypeId, string RegionId), decimal> ReadRegionWeights(
        IGameDataService data,
        string path,
        string idColumn)
    {
        var rows = ReadRows(data, path);
        ExpectHeader(rows, path, $"{idColumn},RegionId,WeightMultiplier");
        var result = new Dictionary<(string TypeId, string RegionId), decimal>(new FlavorRegionComparer());

        for (var i = 1; i < rows.Count; i++)
        {
            var fields = rows[i].Split(',');
            if (fields.Length != 3)
                throw new InvalidOperationException($"{path} row {i + 1} must contain 3 fields.");

            var key = (fields[0].Trim(), fields[1].Trim());
            if (!result.TryAdd(key, ParsePositiveDecimal(fields[2], path, i + 1, "WeightMultiplier")))
                throw new InvalidOperationException($"{path} row {i + 1} duplicates '{key.Item1}/{key.Item2}'.");
        }

        return result;
    }

    private static List<string> ReadRows(IGameDataService data, string path) =>
        data.ReadText(path)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

    private static void ExpectHeader(IReadOnlyList<string> rows, string path, string expected)
    {
        if (rows.Count < 2
            || !rows[0].TrimStart('\uFEFF').Equals(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{path} has an unexpected header. Expected '{expected}'.");
        }
    }

    private static int ParseInt(string text, string path, int row, string field) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"{path} row {row} has invalid {field} '{text}'.");

    private static decimal ParsePositiveDecimal(string text, string path, int row, string field)
    {
        if (!decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            || value <= 0m)
        {
            throw new InvalidOperationException($"{path} row {row} has invalid {field} '{text}'.");
        }

        return value;
    }

    private static FarmingFlavorInfo ToInfo(FlavorDefinition definition) =>
        new(definition.Id, definition.DisplayName, definition.Emoji);

    private sealed record FlavorDefinition(
        string Id,
        string DisplayName,
        string Emoji,
        int StartYear,
        int EndYear,
        decimal BaseWeight);

    private sealed record TimeWeight(
        string TypeId,
        int StartYear,
        int EndYear,
        decimal Multiplier);

    private sealed record WeightedFlavor(
        FlavorDefinition Definition,
        decimal Weight);

    private sealed class FlavorRegionComparer : IEqualityComparer<(string TypeId, string RegionId)>
    {
        public bool Equals((string TypeId, string RegionId) x, (string TypeId, string RegionId) y) =>
            x.TypeId.Equals(y.TypeId, StringComparison.OrdinalIgnoreCase)
            && x.RegionId.Equals(y.RegionId, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string TypeId, string RegionId) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TypeId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.RegionId));
    }
}
