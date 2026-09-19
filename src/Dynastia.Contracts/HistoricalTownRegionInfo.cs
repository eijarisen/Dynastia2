namespace Dynastia.Contracts;

public sealed record HistoricalTownRegionInfo(
    string Id,
    string Name,
    IReadOnlyList<string> JobMarketTags);
