namespace Dynastia.Contracts;

public sealed record StressSnapshot(
    double Total,
    IReadOnlyList<StressContribution> Contributions);
