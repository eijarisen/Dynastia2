using Dynastia.Contracts;

namespace Dynastia.Mechanics.Historical;

internal sealed class HistoricalEventScopeResolver
{
    private readonly HistoricalEventCatalog _catalog;
    private readonly IHistoricalTownCatalog _towns;

    public HistoricalEventScopeResolver(
        HistoricalEventCatalog catalog,
        IHistoricalTownCatalog towns)
    {
        _catalog = catalog;
        _towns = towns;
    }

    public double GetMultiplier(
        string scopeId,
        string placeId,
        int eventYear)
    {
        if (!_catalog.Scopes.TryGetValue(scopeId, out var scope))
            return 0;

        return Resolve(scope, placeId, eventYear, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private double Resolve(
        HistoricalScopeDefinition scope,
        string placeId,
        int eventYear,
        HashSet<string> stack)
    {
        if (!stack.Add(scope.Id))
            throw new InvalidDataException($"Historical event scope cycle detected at '{scope.Id}'.");

        try
        {
            return scope.Type.ToLowerInvariant() switch
            {
                "all_places" => 1.0,
                "regions" => ResolveRegion(scope, placeId, eventYear),
                "places" => scope.PlaceMultipliers is not null
                    && scope.PlaceMultipliers.TryGetValue(placeId, out var placeMultiplier)
                        ? placeMultiplier
                        : 0.0,
                "polity_snapshot" => ResolvePolitySnapshot(scope, placeId, scope.Year ?? eventYear),
                "current_event_polity" => ResolvePolitySnapshot(scope, placeId, eventYear),
                "polity_change" => ResolvePolityChange(scope, placeId),
                "intersection" => ResolveIntersection(scope, placeId, eventYear, stack),
                "union" => ResolveUnion(scope, placeId, eventYear, stack),
                _ => throw new InvalidDataException($"Unsupported historical event scope type '{scope.Type}' for '{scope.Id}'.")
            };
        }
        finally
        {
            stack.Remove(scope.Id);
        }
    }

    private double ResolveRegion(
        HistoricalScopeDefinition scope,
        string placeId,
        int year)
    {
        var town = _towns.GetTown(placeId, year);
        if (town is null || string.IsNullOrWhiteSpace(town.RegionId) || scope.RegionMultipliers is null)
            return 0;

        return scope.RegionMultipliers.TryGetValue(town.RegionId, out var value)
            ? value
            : 0.0;
    }

    private double ResolvePolitySnapshot(
        HistoricalScopeDefinition scope,
        string placeId,
        int year)
    {
        var town = _towns.GetTown(placeId, year);
        if (town is null || scope.PolityIds is null)
            return 0;

        return scope.PolityIds.Contains(town.PolityId, StringComparer.OrdinalIgnoreCase)
            ? 1.0
            : 0.0;
    }

    private double ResolvePolityChange(
        HistoricalScopeDefinition scope,
        string placeId)
    {
        var year = scope.Year
            ?? throw new InvalidDataException($"Historical polity_change scope '{scope.Id}' has no year.");
        var before = _towns.GetTown(placeId, year - 1);
        var after = _towns.GetTown(placeId, year);
        if (before is null || after is null || before.PolityId.Equals(after.PolityId, StringComparison.OrdinalIgnoreCase))
            return 0;

        if (scope.FromPolityIds is { Count: > 0 }
            && !scope.FromPolityIds.Contains(before.PolityId, StringComparer.OrdinalIgnoreCase))
            return 0;
        if (scope.ToPolityIds is { Count: > 0 }
            && !scope.ToPolityIds.Contains(after.PolityId, StringComparer.OrdinalIgnoreCase))
            return 0;

        if (scope.FromPolish is bool fromPolish
            && IsPolish(before.PolityId) != fromPolish)
            return 0;
        if (scope.ToPolish is bool toPolish
            && IsPolish(after.PolityId) != toPolish)
            return 0;

        return 1.0;
    }

    private bool IsPolish(string polityId) =>
        _towns.Polities.TryGetValue(polityId, out var polity)
        && polity.IsPolishPolity;

    private double ResolveIntersection(
        HistoricalScopeDefinition scope,
        string placeId,
        int year,
        HashSet<string> stack)
    {
        if (scope.ScopeIds is not { Count: > 0 })
            return 0;

        var multiplier = 1.0;
        foreach (var nestedId in scope.ScopeIds)
        {
            if (!_catalog.Scopes.TryGetValue(nestedId, out var nested))
                return 0;
            var nestedMultiplier = Resolve(nested, placeId, year, stack);
            if (nestedMultiplier <= 0)
                return 0;
            multiplier *= nestedMultiplier;
        }
        return multiplier;
    }

    private double ResolveUnion(
        HistoricalScopeDefinition scope,
        string placeId,
        int year,
        HashSet<string> stack)
    {
        if (scope.ScopeIds is not { Count: > 0 })
            return 0;

        var multiplier = 0.0;
        foreach (var nestedId in scope.ScopeIds)
        {
            if (!_catalog.Scopes.TryGetValue(nestedId, out var nested))
                continue;
            multiplier = Math.Max(multiplier, Resolve(nested, placeId, year, stack));
        }
        return multiplier;
    }
}
