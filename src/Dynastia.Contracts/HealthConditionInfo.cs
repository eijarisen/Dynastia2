namespace Dynastia.Contracts;

public sealed record HealthConditionInfo(
    string Id,
    string Name,
    string Type,
    double HealthImpact);
