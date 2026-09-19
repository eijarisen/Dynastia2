namespace Dynastia.Contracts;

public sealed record TownHistoricalEvent(
    int Year,
    string Type,
    string PlaceId,
    string? RelatedPlaceId = null,
    string? PreviousName = null,
    string? CurrentName = null);
