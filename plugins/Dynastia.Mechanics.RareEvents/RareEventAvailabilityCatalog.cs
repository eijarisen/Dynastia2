using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

// Compatibility facade retained for older tests/callers. Availability now comes
// from the main data-driven Rare Event catalog rather than a separate file.
public sealed class RareEventAvailabilityCatalog
{
    private readonly RareEventCatalog _catalog;

    private RareEventAvailabilityCatalog(RareEventCatalog catalog) => _catalog = catalog;

    public static RareEventAvailabilityCatalog Load(IGameDataService data) =>
        new(RareEventCatalog.Load(data));

    public bool IsAvailable(string eventId, int year)
    {
        var definition = _catalog.Find(eventId)
            ?? throw new InvalidDataException($"Rare event catalog has no event '{eventId}'.");
        return year >= definition.StartYear && (definition.EndYear is null || year <= definition.EndYear.Value);
    }
}
