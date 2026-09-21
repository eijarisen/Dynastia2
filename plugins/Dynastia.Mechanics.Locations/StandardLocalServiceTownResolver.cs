using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

internal sealed class StandardLocalServiceTownResolver : ILocalServiceTownResolver
{
    private readonly HistoricalTownCatalog _catalog;
    private readonly IGameState _gameState;
    private readonly IReadOnlyDictionary<string, string> _rootByPlaceId;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _clusterByRoot;

    public StandardLocalServiceTownResolver(
        HistoricalTownCatalog catalog,
        IGameState gameState)
    {
        _catalog = catalog;
        _gameState = gameState;

        var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var placeId in catalog.PermanentPlaceIds)
            roots[placeId] = ResolveRoot(placeId);

        _rootByPlaceId = roots;
        _clusterByRoot = roots
            .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(pair => pair.Key)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    public TownInfo Resolve(TownInfo town)
    {
        ArgumentNullException.ThrowIfNull(town);
        if (string.IsNullOrWhiteSpace(town.Id))
            return town;

        return TryResolveAnchor(town.Id, _gameState.Year) ?? town;
    }

    public TownInfo Resolve(string placeId, int year) =>
        TryResolveAnchor(placeId, year)
        ?? _catalog.GetTown(placeId, year)
        ?? throw new InvalidOperationException(
            $"Town '{placeId}' cannot be resolved for local services in {year}.");

    public IReadOnlyList<string> GetClusterPlaceIds(string placeId)
    {
        if (string.IsNullOrWhiteSpace(placeId))
            return Array.Empty<string>();

        if (!_rootByPlaceId.TryGetValue(placeId, out var root))
            return new[] { placeId };

        return _clusterByRoot.TryGetValue(root, out var cluster)
            ? cluster
            : new[] { placeId };
    }

    private TownInfo? TryResolveAnchor(string placeId, int year)
    {
        if (!_rootByPlaceId.TryGetValue(placeId, out var root))
            return _catalog.GetTown(placeId, year);

        var candidates = GetClusterPlaceIds(placeId)
            .Select(id => (Id: id, Town: _catalog.GetTown(id, year)))
            .Where(item => item.Town is not null)
            .Select(item => (item.Id, Town: item.Town!))
            .OrderByDescending(item => item.Town.Population)
            .ThenByDescending(item => item.Id.Equals(root, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return candidates.Length == 0
            ? null
            : candidates[0].Town;
    }

    private string ResolveRoot(string placeId)
    {
        var current = placeId;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (visited.Add(current))
        {
            var proxy = _catalog.GetProxyPlaceId(current);
            if (string.IsNullOrWhiteSpace(proxy)
                || !_catalog.PermanentPlaceIds.Contains(proxy, StringComparer.OrdinalIgnoreCase))
            {
                return current;
            }

            current = proxy;
        }

        throw new InvalidDataException(
            $"Historical town proxyPlaceId links contain a cycle at '{current}'.");
    }
}
