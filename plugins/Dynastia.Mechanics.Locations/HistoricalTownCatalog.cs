using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed class HistoricalTownCatalog : IHistoricalTownCatalog
{
    private const string DataPath = "Towns/dynastia-towns.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IReadOnlyDictionary<string, PlaceRecord> _places;
    private readonly IReadOnlyDictionary<string, PeriodSeries<NamePeriod>> _names;
    private readonly IReadOnlyDictionary<string, PeriodSeries<TerritoryPeriod>> _territories;
    private readonly IReadOnlyDictionary<string, PeriodSeries<PolandPeriod>> _polandPeriods;
    private readonly IReadOnlyDictionary<string, PeriodSeries<StatusPeriod>> _townStatus;
    private readonly IReadOnlyDictionary<string, PeriodSeries<SimplePeriod>> _playablePeriods;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<PopulationSnapshot>> _population;
    private readonly IReadOnlyDictionary<string, CountyRecord> _counties;
    private readonly IReadOnlyList<MergerRecord> _automaticMergers;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<TownHistoricalEvent>> _events;

    private HistoricalTownCatalog(Bundle bundle)
    {
        MinYear = bundle.Manifest.MinYear;
        MaxYear = bundle.Manifest.MaxYear;

        _places = bundle.Towns.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        _counties = bundle.Counties.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        Regions = bundle.Regions.ToDictionary(
            item => item.Id,
            item => new HistoricalTownRegionInfo(
                item.Id,
                item.Name,
                item.JobMarketTags ?? []),
            StringComparer.OrdinalIgnoreCase);

        Polities = bundle.Polities.ToDictionary(
            item => item.Id,
            item => new HistoricalPolityInfo(
                item.Id,
                item.Name,
                item.IsPolishPolity,
                item.IsSovereignPolishState,
                item.OverlordId),
            StringComparer.OrdinalIgnoreCase);

        _names = ToSeries(bundle.Names, item => item.PlaceId, item => item.Periods);
        _territories = ToSeries(bundle.Territories, item => item.PlaceId, item => item.Periods);
        _polandPeriods = ToSeries(bundle.PolandPeriods, item => item.PlaceId, item => item.Periods);
        _townStatus = ToSeries(bundle.TownStatus, item => item.PlaceId, item => item.Periods);
        _playablePeriods = ToSeries(bundle.PlayablePeriods, item => item.PlaceId, item => item.Periods);
        _population = bundle.Population.ToDictionary(
            item => item.PlaceId,
            item => (IReadOnlyList<PopulationSnapshot>)item.GameSnapshots
                .OrderBy(snapshot => snapshot.Year)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);

        _automaticMergers = bundle.Mergers
            .Where(item => item.Automatic && item.From is not null)
            .OrderBy(item => item.From)
            .ToList();

        Validate(bundle);
        _events = BuildEvents(bundle);
    }

    public int MinYear { get; }

    public int MaxYear { get; }

    public int PermanentPlaceCount => _places.Count;

    public IReadOnlyCollection<string> PermanentPlaceIds =>
        _places.Keys.ToArray();

    public IReadOnlyDictionary<string, HistoricalTownRegionInfo> Regions { get; }

    public IReadOnlyDictionary<string, HistoricalPolityInfo> Polities { get; }

    internal string? GetProxyPlaceId(string placeId) =>
        _places.TryGetValue(placeId, out var place)
            ? place.Coordinates.ProxyPlaceId
            : null;

    public static HistoricalTownCatalog Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var bundle = CatalogValidation.DeserializeJson<Bundle>(
            data,
            DataPath,
            JsonOptions);

        return new HistoricalTownCatalog(bundle);
    }

    public TownInfo? GetTown(string placeId, int year)
    {
        if (string.IsNullOrWhiteSpace(placeId)
            || !_places.TryGetValue(placeId, out var place))
        {
            return null;
        }

        var effectiveYear = EffectiveYear(year);
        var name = ResolveRequired(_names, placeId, effectiveYear)?.Name
            ?? place.ReferenceName;
        var territory = ResolveRequired(_territories, placeId, effectiveYear);
        var polityId = territory?.PolityId ?? string.Empty;
        var polityName = Polities.TryGetValue(polityId, out var polity)
            ? polity.Name
            : polityId;
        var status = ResolveRequired(_townStatus, placeId, effectiveYear)?.Status
            ?? "rural_or_unrecorded";
        var population = ResolvePopulation(placeId, effectiveYear);
        var county = _counties[place.CountyId].Name;

        return new TownInfo(
            name,
            county,
            place.Coordinates.Longitude,
            place.Coordinates.Latitude,
            population)
        {
            Id = place.Id,
            RegionId = place.RegionId,
            PolityId = polityId,
            PolityName = polityName,
            UrbanStatus = status,
            IsDestinationAvailable = IsAvailable(
                place.Id,
                effectiveYear,
                TownMapMode.PolishHistoryContinuity,
                status)
        };
    }

    public IReadOnlyList<TownInfo> GetAvailableTowns(
        int year,
        TownMapMode mode = TownMapMode.PolishHistoryContinuity)
    {
        var effectiveYear = EffectiveYear(year);
        var result = new List<TownInfo>();

        foreach (var place in _places.Values)
        {
            var status = ResolveRequired(_townStatus, place.Id, effectiveYear)?.Status
                ?? "rural_or_unrecorded";

            if (!IsAvailable(place.Id, effectiveYear, mode, status))
                continue;

            var town = GetTown(place.Id, effectiveYear);
            if (town is null)
                continue;

            result.Add(town with { IsDestinationAvailable = true });
        }

        return result
            .OrderBy(town => town.Town, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(town => town.County, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(town => town.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string? ResolveMunicipality(string placeId, int year)
    {
        if (string.IsNullOrWhiteSpace(placeId) || !_places.ContainsKey(placeId))
            return null;

        var effectiveYear = EffectiveYear(year);
        var current = placeId;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (visited.Add(current))
        {
            var merger = _automaticMergers.FirstOrDefault(item =>
                item.FromPlaceId.Equals(current, StringComparison.OrdinalIgnoreCase)
                && IsInPeriod(effectiveYear, item.From!.Value, item.Until));

            if (merger is null)
                return current;

            current = merger.ToPlaceId;
        }

        throw new InvalidDataException(
            $"{DataPath}: automatic municipality links contain a cycle at '{current}'.");
    }

    public IReadOnlyList<TownHistoricalEvent> GetEvents(int year)
    {
        if (year > MaxYear)
            return [];

        var effectiveYear = EffectiveYear(year);
        return _events.TryGetValue(effectiveYear, out var events)
            ? events
            : [];
    }

    private int EffectiveYear(int year)
    {
        if (year < MinYear)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year),
                year,
                $"Historical town data begins in {MinYear}.");
        }

        return Math.Min(year, MaxYear);
    }

    private bool IsAvailable(
        string placeId,
        int effectiveYear,
        TownMapMode mode,
        string status)
    {
        if (!IsUrbanDestinationStatus(status))
            return false;

        return mode switch
        {
            TownMapMode.AllPlaces => true,
            TownMapMode.PolishPolities =>
                ResolveRequired(_polandPeriods, placeId, effectiveYear) is not null,
            TownMapMode.PolishHistoryContinuity =>
                ResolveRequired(_playablePeriods, placeId, effectiveYear) is not null,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private static bool IsUrbanDestinationStatus(string status) =>
        status.Equals("town", StringComparison.OrdinalIgnoreCase)
        || status.Equals("historic_market_town", StringComparison.OrdinalIgnoreCase);

    private int ResolvePopulation(string placeId, int year)
    {
        var snapshots = _population[placeId];
        if (snapshots.Count == 0)
            throw new InvalidDataException($"{DataPath}: place '{placeId}' has no game population snapshots.");

        var exact = snapshots.FirstOrDefault(snapshot => snapshot.Year == year);
        if (exact is not null)
            return exact.Population;

        PopulationSnapshot? lower = null;
        PopulationSnapshot? upper = null;

        foreach (var snapshot in snapshots)
        {
            if (snapshot.Year < year)
                lower = snapshot;
            else if (snapshot.Year > year)
            {
                upper = snapshot;
                break;
            }
        }

        if (lower is null)
            return snapshots[0].Population;
        if (upper is null)
            return snapshots[^1].Population;

        var span = upper.Year - lower.Year;
        if (span <= 0 || lower.Population <= 0 || upper.Population <= 0)
            return lower.Population;

        var fraction = (year - lower.Year) / (double)span;
        var value = lower.Population
            * Math.Pow(upper.Population / (double)lower.Population, fraction);

        return Math.Max(
            1,
            (int)Math.Round(value, 0, MidpointRounding.AwayFromZero));
    }

    private static TPeriod? ResolveRequired<TPeriod>(
        IReadOnlyDictionary<string, PeriodSeries<TPeriod>> series,
        string placeId,
        int year)
        where TPeriod : PeriodBase
    {
        if (!series.TryGetValue(placeId, out var row))
            return null;

        return row.Periods.FirstOrDefault(period =>
            IsInPeriod(year, period.From, period.Until));
    }

    private static bool IsInPeriod(int year, int from, int? until) =>
        year >= from && (until is null || year < until.Value);

    private static IReadOnlyDictionary<string, PeriodSeries<TPeriod>> ToSeries<TSource, TPeriod>(
        IEnumerable<TSource> source,
        Func<TSource, string> idSelector,
        Func<TSource, List<TPeriod>> periodSelector)
        where TPeriod : PeriodBase =>
        source.ToDictionary(
            idSelector,
            item => new PeriodSeries<TPeriod>(periodSelector(item)),
            StringComparer.OrdinalIgnoreCase);

    private void Validate(Bundle bundle)
    {
        if (MinYear != 1700 || MaxYear != 2027)
        {
            throw CatalogValidation.Error(
                DataPath,
                "manifest years 1700 through 2027",
                field: "manifest.minYear/maxYear",
                value: $"{MinYear}-{MaxYear}");
        }

        if (bundle.Manifest.Counts.Places <= 0
            || _places.Count != bundle.Manifest.Counts.Places)
        {
            throw CatalogValidation.Error(
                DataPath,
                "a positive towns.Count matching manifest.counts.places",
                field: "towns.Count",
                value: $"{_places.Count} (manifest {bundle.Manifest.Counts.Places})");
        }

        if (bundle.Manifest.Counts.Regions <= 0
            || Regions.Count != bundle.Manifest.Counts.Regions)
        {
            throw CatalogValidation.Error(
                DataPath,
                "a positive regions.Count matching manifest.counts.regions",
                field: "regions.Count",
                value: $"{Regions.Count} (manifest {bundle.Manifest.Counts.Regions})");
        }

        ValidateCompleteSeries(_names, "names");
        ValidateCompleteSeries(_territories, "territories");
        ValidateCompleteSeries(_polandPeriods, "polandPeriods");
        ValidateCompleteSeries(_townStatus, "townStatus");
        ValidateCompleteSeries(_playablePeriods, "playablePeriods");

        if (_population.Count != _places.Count)
        {
            throw CatalogValidation.Error(
                DataPath,
                "one population row for every permanent place",
                field: "population.Count",
                value: _population.Count);
        }

        foreach (var place in _places.Values)
        {
            if (!_counties.ContainsKey(place.CountyId))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a countyId defined in counties",
                    item: place.Id,
                    field: "countyId",
                    value: place.CountyId);
            }

            if (!Regions.ContainsKey(place.RegionId))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a regionId defined in regions",
                    item: place.Id,
                    field: "regionId",
                    value: place.RegionId);
            }

            if (!double.IsFinite(place.Coordinates.Latitude)
                || !double.IsFinite(place.Coordinates.Longitude))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "finite WGS84 coordinates",
                    item: place.Id,
                    field: "coordinates",
                    value: $"{place.Coordinates.Latitude},{place.Coordinates.Longitude}");
            }

            if (!_population.TryGetValue(place.Id, out var snapshots)
                || snapshots.Count == 0
                || snapshots.Any(snapshot => snapshot.Population <= 0)
                || snapshots.Zip(snapshots.Skip(1), (left, right) => left.Year < right.Year).Any(valid => !valid))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "strictly ordered positive game population snapshots",
                    item: place.Id,
                    field: "population.gameSnapshots",
                    value: snapshots?.Count ?? 0);
            }
        }

        foreach (var territory in bundle.Territories.SelectMany(row => row.Periods))
        {
            if (!Polities.ContainsKey(territory.PolityId))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a polityId defined in polities",
                    field: "territories.polityId",
                    value: territory.PolityId);
            }
        }

        foreach (var merger in bundle.Mergers)
        {
            if (!_places.ContainsKey(merger.FromPlaceId)
                || !_places.ContainsKey(merger.ToPlaceId))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "merger place IDs defined in towns",
                    item: merger.Id,
                    field: "mergers.fromPlaceId/toPlaceId",
                    value: $"{merger.FromPlaceId}->{merger.ToPlaceId}");
            }
        }
    }

    private void ValidateCompleteSeries<TPeriod>(
        IReadOnlyDictionary<string, PeriodSeries<TPeriod>> series,
        string field)
        where TPeriod : PeriodBase
    {
        if (series.Count != _places.Count
            || _places.Keys.Any(id => !series.ContainsKey(id)))
        {
            throw CatalogValidation.Error(
                DataPath,
                "one row for every permanent place",
                field: $"{field}.Count",
                value: series.Count);
        }

        foreach (var (placeId, row) in series)
        {
            var ordered = row.Periods.OrderBy(period => period.From).ToList();
            for (var index = 0; index < ordered.Count; index++)
            {
                var period = ordered[index];
                if (period.From < MinYear
                    || period.From > MaxYear
                    || period.Until is int until && until <= period.From)
                {
                    throw CatalogValidation.Error(
                        DataPath,
                        "a valid inclusive/exclusive historical interval",
                        item: placeId,
                        field: field,
                        value: $"[{period.From},{period.Until?.ToString() ?? "null"})");
                }

                if (index > 0)
                {
                    var previous = ordered[index - 1];
                    if (previous.Until is null || previous.Until > period.From)
                    {
                        throw CatalogValidation.Error(
                            DataPath,
                            "non-overlapping periods",
                            item: placeId,
                            field: field,
                            value: $"{previous.From}/{previous.Until}->{period.From}/{period.Until}");
                    }
                }
            }
        }
    }

    private IReadOnlyDictionary<int, IReadOnlyList<TownHistoricalEvent>> BuildEvents(Bundle bundle)
    {
        var events = new Dictionary<int, List<TownHistoricalEvent>>();

        foreach (var row in bundle.Names)
        {
            var ordered = row.Periods.OrderBy(period => period.From).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                if (previous.Name.Equals(current.Name, StringComparison.Ordinal)
                    || !current.Quality.Equals(
                        "curated_annual_rename",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddEvent(
                    events,
                    new TownHistoricalEvent(
                        current.From,
                        "rename",
                        row.PlaceId,
                        PreviousName: previous.Name,
                        CurrentName: current.Name));
            }
        }

        foreach (var merger in bundle.Mergers.Where(item => item.Automatic && item.From is not null))
        {
            AddEvent(
                events,
                new TownHistoricalEvent(
                    merger.From!.Value,
                    "merge",
                    merger.FromPlaceId,
                    merger.ToPlaceId));

            if (merger.Until is int until && until <= MaxYear)
            {
                AddEvent(
                    events,
                    new TownHistoricalEvent(
                        until,
                        "separation",
                        merger.FromPlaceId,
                        merger.ToPlaceId));
            }
        }

        return events.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<TownHistoricalEvent>)pair.Value
                .OrderBy(item => item.PlaceId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Type, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static void AddEvent(
        IDictionary<int, List<TownHistoricalEvent>> events,
        TownHistoricalEvent item)
    {
        if (!events.TryGetValue(item.Year, out var list))
        {
            list = [];
            events[item.Year] = list;
        }

        list.Add(item);
    }

    private sealed record PeriodSeries<TPeriod>(IReadOnlyList<TPeriod> Periods)
        where TPeriod : PeriodBase;

    private abstract class PeriodBase
    {
        public int From { get; set; }
        public int? Until { get; set; }
    }

    private sealed class Bundle
    {
        public Bundle() { }
        public ManifestRecord Manifest { get; set; } = new();
        public List<PlaceRecord> Towns { get; set; } = [];
        public List<NameSeriesRecord> Names { get; set; } = [];
        public List<TerritorySeriesRecord> Territories { get; set; } = [];
        public List<PolandSeriesRecord> PolandPeriods { get; set; } = [];
        public List<StatusSeriesRecord> TownStatus { get; set; } = [];
        public List<PlayableSeriesRecord> PlayablePeriods { get; set; } = [];
        public List<PopulationSeriesRecord> Population { get; set; } = [];
        public List<MergerRecord> Mergers { get; set; } = [];
        public List<RegionRecord> Regions { get; set; } = [];
        public List<CountyRecord> Counties { get; set; } = [];
        public List<PolityRecord> Polities { get; set; } = [];
    }

    private sealed class ManifestRecord
    {
        public ManifestRecord() { }
        public int MinYear { get; set; }
        public int MaxYear { get; set; }
        public ManifestCountsRecord Counts { get; set; } = new();
    }

    private sealed class ManifestCountsRecord
    {
        public ManifestCountsRecord() { }
        public int Places { get; set; }
        public int Regions { get; set; }
    }

    private sealed class PlaceRecord
    {
        public PlaceRecord() { }
        public string Id { get; set; } = string.Empty;
        public string ReferenceName { get; set; } = string.Empty;
        public string CountyId { get; set; } = string.Empty;
        public string RegionId { get; set; } = string.Empty;
        public CoordinateRecord Coordinates { get; set; } = new();
    }

    private sealed class CoordinateRecord
    {
        public CoordinateRecord() { }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? ProxyPlaceId { get; set; }
    }

    private sealed class NameSeriesRecord
    {
        public NameSeriesRecord() { }
        public string PlaceId { get; set; } = string.Empty;
        public List<NamePeriod> Periods { get; set; } = [];
    }

    private sealed class NamePeriod : PeriodBase
    {
        public NamePeriod() { }
        public string Name { get; set; } = string.Empty;
        public string Quality { get; set; } = string.Empty;
    }

    private sealed class TerritorySeriesRecord
    {
        public TerritorySeriesRecord() { }
        public string PlaceId { get; set; } = string.Empty;
        public List<TerritoryPeriod> Periods { get; set; } = [];
    }

    private sealed class TerritoryPeriod : PeriodBase
    {
        public TerritoryPeriod() { }
        public string PolityId { get; set; } = string.Empty;
    }

    private sealed class PolandSeriesRecord
    {
        public PolandSeriesRecord() { }
        public string PlaceId { get; set; } = string.Empty;
        public List<PolandPeriod> Periods { get; set; } = [];
    }

    private sealed class PolandPeriod : PeriodBase
    {
        public PolandPeriod() { }
        public string PolityId { get; set; } = string.Empty;
    }

    private sealed class StatusSeriesRecord
    {
        public StatusSeriesRecord() { }
        public string PlaceId { get; set; } = string.Empty;
        public List<StatusPeriod> Periods { get; set; } = [];
    }

    private sealed class StatusPeriod : PeriodBase
    {
        public StatusPeriod() { }
        public string Status { get; set; } = string.Empty;
    }

    private sealed class PlayableSeriesRecord
    {
        public PlayableSeriesRecord() { }
        public string PlaceId { get; set; } = string.Empty;
        public List<SimplePeriod> Periods { get; set; } = [];
    }

    private sealed class SimplePeriod : PeriodBase
    {
        public SimplePeriod() { }
    }

    private sealed class PopulationSeriesRecord
    {
        public PopulationSeriesRecord() { }
        public string PlaceId { get; set; } = string.Empty;
        public List<PopulationSnapshot> GameSnapshots { get; set; } = [];
    }

    private sealed class PopulationSnapshot
    {
        public PopulationSnapshot() { }
        public int Year { get; set; }
        public int Population { get; set; }
    }

    private sealed class MergerRecord
    {
        public MergerRecord() { }
        public string Id { get; set; } = string.Empty;
        public string FromPlaceId { get; set; } = string.Empty;
        public string ToPlaceId { get; set; } = string.Empty;
        public int? From { get; set; }
        public int? Until { get; set; }
        public bool Automatic { get; set; }
    }

    private sealed class RegionRecord
    {
        public RegionRecord() { }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string>? JobMarketTags { get; set; }
    }

    private sealed class CountyRecord
    {
        public CountyRecord() { }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private sealed class PolityRecord
    {
        public PolityRecord() { }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsPolishPolity { get; set; }
        public bool IsSovereignPolishState { get; set; }
        public string? OverlordId { get; set; }
    }
}
