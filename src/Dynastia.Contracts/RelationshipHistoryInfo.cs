namespace Dynastia.Contracts;

public sealed record RelationshipHistoryInfo(
    Guid SpouseId,
    int StartYear,
    int? EndYear,
    string? EndReason);
