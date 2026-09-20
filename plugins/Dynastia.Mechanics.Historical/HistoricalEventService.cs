using Dynastia.Contracts;

namespace Dynastia.Mechanics.Historical;

internal sealed class HistoricalEventService :
    IHistoricalEventService,
    INationalityDistributionModifierProvider
{
    private readonly HistoricalEventCatalog _catalog;
    private readonly HistoricalEventScopeResolver _scopes;
    private readonly IHistoricalTownCatalog _towns;

    public HistoricalEventService(
        HistoricalEventCatalog catalog,
        IHistoricalTownCatalog towns)
    {
        _catalog = catalog;
        _towns = towns;
        _scopes = new HistoricalEventScopeResolver(catalog, towns);
    }

    internal HistoricalEventCatalog Catalog => _catalog;
    internal HistoricalEventScopeResolver Scopes => _scopes;

    public bool IsEventActive(string eventId, int year) =>
        _catalog.Events.Any(item =>
            item.Id.Equals(eventId, StringComparison.OrdinalIgnoreCase)
            && HistoricalEventContentConfiguration.IsEnabled(item)
            && item.IsActive(year));


    public IReadOnlyCollection<string> GetAffectedPlaceIds(
        string eventId,
        int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var historicalEvent = _catalog.Events.FirstOrDefault(item =>
            item.Id.Equals(eventId, StringComparison.OrdinalIgnoreCase)
            && HistoricalEventContentConfiguration.IsEnabled(item)
            && item.IsActive(year));
        if (historicalEvent is null)
            return Array.Empty<string>();

        // Resolve against the towns that actually exist as gameplay locations
        // in the event year. Broad scopes such as all_places must not seed
        // prosperity state for future/non-urban permanent place IDs.
        return _towns.GetAvailableTowns(year)
            .Where(town => _scopes.GetMultiplier(historicalEvent.ScopeId, town.Id, year) > 0)
            .Select(town => town.Id)
            .ToArray();
    }

    public int? GetEventStartYear(string eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        return _catalog.Events
            .FirstOrDefault(item =>
                item.Id.Equals(eventId, StringComparison.OrdinalIgnoreCase)
                && HistoricalEventContentConfiguration.IsEnabled(item))
            ?.StartYear;
    }

    public HistoricalResidenceSnapshot? GetExternalResidence(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);
        var state = person.Components.Get<ExternalResidenceComponent>();
        return state is null
            ? null
            : new HistoricalResidenceSnapshot(
                state.DestinationLabel,
                state.DepartureYear,
                state.CauseEventId,
                state.Forced);
    }

    public IReadOnlyDictionary<string, double> Apply(
        string regionId,
        int year,
        IReadOnlyDictionary<string, double> distribution)
    {
        var result = new Dictionary<string, double>(distribution, StringComparer.OrdinalIgnoreCase);
        var matching = _catalog.CandidateModifiers
            .Where(item => item.IsActive(year) && IsEventActive(item.EventId, year))
            .Where(item => ScopeMatchesRegion(item.ScopeId, regionId, year))
            .GroupBy(item => item.NationalityId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Priority).First())
            .ToList();

        if (matching.Count == 0)
            return result;

        foreach (var modifier in matching)
        {
            if (result.TryGetValue(modifier.NationalityId, out var weight))
                result[modifier.NationalityId] = Math.Max(0, weight * modifier.WeightMultiplier);
        }

        var total = result.Values.Where(value => value > 0).Sum();
        if (total <= 0)
            return distribution;

        foreach (var key in result.Keys.ToList())
            result[key] = Math.Max(0, result[key]) / total * 100.0;

        return result;
    }

    private bool ScopeMatchesRegion(string scopeId, string regionId, int year)
    {
        if (!_catalog.Scopes.TryGetValue(scopeId, out var scope))
            return false;

        if (scope.Type.Equals("regions", StringComparison.OrdinalIgnoreCase))
            return scope.RegionMultipliers?.ContainsKey(regionId) == true;

        if (scope.Type.Equals("current_event_polity", StringComparison.OrdinalIgnoreCase)
            || scope.Type.Equals("all_places", StringComparison.OrdinalIgnoreCase))
            return true;

        // Candidate modifier scopes in the supplied catalog are region/current-Poland
        // scopes. Fail closed if future authoring introduces another form.
        return false;
    }
}
