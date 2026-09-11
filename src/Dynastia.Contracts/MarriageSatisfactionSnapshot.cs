namespace Dynastia.Contracts;

public sealed record MarriageSatisfactionSnapshot(
    Guid SpouseId,
    double Value,
    string Label,
    int StartYear,
    IReadOnlyList<string> CurrentIssues);
