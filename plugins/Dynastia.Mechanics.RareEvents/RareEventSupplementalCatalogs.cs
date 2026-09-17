using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

public sealed record RareEventEpidemicCondition(
    string ConditionId,
    int StartYear,
    int? EndYear,
    double BaseWeight,
    int MinimumAffected,
    int MaximumAffected)
{
    public bool IsAvailable(int year) =>
        year >= StartYear && (EndYear is null || year <= EndYear.Value);
}

public sealed class RareEventEpidemicCatalog
{
    private const string Path = "RareEvents/rare_event_epidemic_conditions.csv";

    private RareEventEpidemicCatalog(IReadOnlyList<RareEventEpidemicCondition> entries) =>
        Entries = entries;

    public IReadOnlyList<RareEventEpidemicCondition> Entries { get; }

    public static RareEventEpidemicCatalog Load(IGameDataService data)
    {
        var lines = data.ReadText(Path)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header =
            "ConditionId,StartYear,EndYear,BaseWeight,MinimumAffected,MaximumAffected";

        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                Path,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var list = new List<RareEventEpidemicCondition>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 6)
                throw CatalogValidation.FieldCount(Path, row, fields.Length, 6);

            var conditionId = fields[0].Trim();
            if (string.IsNullOrWhiteSpace(conditionId))
            {
                throw CatalogValidation.Error(
                    Path,
                    "a non-empty condition ID",
                    row,
                    field: "ConditionId",
                    value: conditionId);
            }

            var start = CatalogValidation.ParseInt(Path, row, "StartYear", fields[1]);
            var end = string.IsNullOrWhiteSpace(fields[2])
                ? (int?)null
                : CatalogValidation.ParseInt(Path, row, "EndYear", fields[2]);
            var weight = CatalogValidation.ParseDouble(Path, row, "BaseWeight", fields[3]);
            var minimumAffected = CatalogValidation.ParseInt(Path, row, "MinimumAffected", fields[4]);
            var maximumAffected = CatalogValidation.ParseInt(Path, row, "MaximumAffected", fields[5]);

            if (start < GameCalendarConfiguration.GameStartYear)
            {
                throw CatalogValidation.Error(
                    Path,
                    $"a year at or after {GameCalendarConfiguration.GameStartYear}",
                    row,
                    conditionId,
                    "StartYear",
                    start);
            }

            if (end is int endYear && endYear < start)
            {
                throw CatalogValidation.Error(
                    Path,
                    $"a year at or after StartYear ({start})",
                    row,
                    conditionId,
                    "EndYear",
                    endYear);
            }

            if (weight <= 0)
            {
                throw CatalogValidation.Error(
                    Path,
                    "a number greater than 0",
                    row,
                    conditionId,
                    "BaseWeight",
                    weight);
            }

            if (minimumAffected < 1)
            {
                throw CatalogValidation.Error(
                    Path,
                    "an integer of at least 1",
                    row,
                    conditionId,
                    "MinimumAffected",
                    minimumAffected);
            }

            if (maximumAffected < minimumAffected)
            {
                throw CatalogValidation.Error(
                    Path,
                    $"an integer of at least MinimumAffected ({minimumAffected})",
                    row,
                    conditionId,
                    "MaximumAffected",
                    maximumAffected);
            }

            list.Add(new RareEventEpidemicCondition(
                conditionId,
                start,
                end,
                weight,
                minimumAffected,
                maximumAffected));
        }

        return new RareEventEpidemicCatalog(list);
    }
}

public sealed class RareEventCareerFamilyWeightCatalog
{
    private const string Path = "RareEvents/rare_event_career_family_weights.csv";
    private readonly Dictionary<(string EventId, string Family), double> _weights;

    private RareEventCareerFamilyWeightCatalog(
        Dictionary<(string, string), double> weights) =>
        _weights = weights;

    public double GetMultiplier(string eventId, string? careerFamily) =>
        string.IsNullOrWhiteSpace(careerFamily)
            ? 1.0
            : _weights.TryGetValue(
                (eventId.ToUpperInvariant(), careerFamily.ToUpperInvariant()),
                out var multiplier)
                ? multiplier
                : 1.0;

    public static RareEventCareerFamilyWeightCatalog Load(
        IGameDataService data,
        RareEventCatalog events,
        IReadOnlySet<string> knownCareerFamilies)
    {
        var lines = data.ReadText(Path)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "EventId,CareerFamily,WeightMultiplier";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                Path,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var knownEvents = events.Events
            .Select(item => item.EventId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var weights = new Dictionary<(string, string), double>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 3)
                throw CatalogValidation.FieldCount(Path, row, fields.Length, 3);

            var eventId = fields[0].Trim();
            var family = fields[1].Trim();
            if (!knownEvents.Contains(eventId))
            {
                throw CatalogValidation.Error(
                    Path,
                    "an EventId present in rare_events.csv",
                    row,
                    field: "EventId",
                    value: eventId);
            }

            if (!knownCareerFamilies.Contains(family))
            {
                throw CatalogValidation.Error(
                    Path,
                    "a known CareerFamily",
                    row,
                    eventId,
                    "CareerFamily",
                    family);
            }

            var multiplier = CatalogValidation.ParseDouble(
                Path,
                row,
                "WeightMultiplier",
                fields[2]);
            if (multiplier <= 0)
            {
                throw CatalogValidation.Error(
                    Path,
                    "a number greater than 0",
                    row,
                    eventId,
                    "WeightMultiplier",
                    multiplier);
            }

            var key = (eventId.ToUpperInvariant(), family.ToUpperInvariant());
            if (!weights.TryAdd(key, multiplier))
            {
                throw CatalogValidation.Error(
                    Path,
                    "a unique EventId/CareerFamily pair",
                    row,
                    eventId,
                    "CareerFamily",
                    family);
            }
        }

        return new RareEventCareerFamilyWeightCatalog(weights);
    }
}

public sealed record RareEventVariant(
    string EventId,
    int StartYear,
    int? EndYear,
    string DisplayName)
{
    public bool Covers(int year) =>
        year >= StartYear && (EndYear is null || year <= EndYear.Value);
}

public sealed class RareEventVariantCatalog
{
    private const string Path = "RareEvents/rare_event_variants.csv";
    private readonly IReadOnlyDictionary<string, IReadOnlyList<RareEventVariant>> _variants;

    private RareEventVariantCatalog(IReadOnlyList<RareEventVariant> variants) =>
        _variants = variants
            .GroupBy(variant => variant.EventId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RareEventVariant>)group
                    .OrderBy(variant => variant.StartYear)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

    public string ResolveName(RareEventDefinition definition, int year) =>
        _variants.TryGetValue(definition.EventId, out var rows)
            ? rows.FirstOrDefault(row => row.Covers(year))?.DisplayName ?? definition.Name
            : definition.Name;

    public static RareEventVariantCatalog Load(
        IGameDataService data,
        RareEventCatalog events)
    {
        var lines = data.ReadText(Path)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "EventId,StartYear,EndYear,DisplayName";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                Path,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var known = events.Events
            .Select(item => item.EventId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var parsed = new List<(RareEventVariant Variant, int Row)>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 4)
                throw CatalogValidation.FieldCount(Path, row, fields.Length, 4);

            var eventId = fields[0].Trim();
            if (!known.Contains(eventId))
            {
                throw CatalogValidation.Error(
                    Path,
                    "an EventId present in rare_events.csv",
                    row,
                    field: "EventId",
                    value: eventId);
            }

            var start = CatalogValidation.ParseInt(Path, row, "StartYear", fields[1]);
            var end = string.IsNullOrWhiteSpace(fields[2])
                ? (int?)null
                : CatalogValidation.ParseInt(Path, row, "EndYear", fields[2]);
            if (end is int endYear && endYear < start)
            {
                throw CatalogValidation.Error(
                    Path,
                    $"a year at or after StartYear ({start})",
                    row,
                    eventId,
                    "EndYear",
                    endYear);
            }

            var displayName = fields[3].Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw CatalogValidation.Error(
                    Path,
                    "a non-empty display name",
                    row,
                    eventId,
                    "DisplayName",
                    displayName);
            }

            parsed.Add((
                new RareEventVariant(eventId, start, end, displayName),
                row));
        }

        foreach (var group in parsed.GroupBy(
            entry => entry.Variant.EventId,
            StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group
                .OrderBy(entry => entry.Variant.StartYear)
                .ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                if (previous.Variant.EndYear is null
                    || previous.Variant.EndYear.Value >= current.Variant.StartYear)
                {
                    throw CatalogValidation.Error(
                        Path,
                        $"a year range that does not overlap row {previous.Row}",
                        current.Row,
                        current.Variant.EventId,
                        "StartYear",
                        current.Variant.StartYear);
                }
            }
        }

        return new RareEventVariantCatalog(
            parsed.Select(entry => entry.Variant).ToList());
    }
}
