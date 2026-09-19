namespace Dynastia.Contracts;

public interface IHistoricalTownCatalog
{
    int MinYear { get; }
    int MaxYear { get; }
    int PermanentPlaceCount { get; }

    IReadOnlyDictionary<string, HistoricalTownRegionInfo> Regions { get; }
    IReadOnlyDictionary<string, HistoricalPolityInfo> Polities { get; }

    TownInfo? GetTown(string placeId, int year);

    IReadOnlyList<TownInfo> GetAvailableTowns(
        int year,
        TownMapMode mode = TownMapMode.PolishHistoryContinuity);

    string? ResolveMunicipality(string placeId, int year);

    IReadOnlyList<TownHistoricalEvent> GetEvents(int year);
}
