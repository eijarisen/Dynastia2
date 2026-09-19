namespace Dynastia.Contracts;

public sealed record HistoricalPolityInfo(
    string Id,
    string Name,
    bool IsPolishPolity,
    bool IsSovereignPolishState,
    string? OverlordId);
