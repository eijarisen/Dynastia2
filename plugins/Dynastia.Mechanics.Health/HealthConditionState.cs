namespace Dynastia.Mechanics.Health;

public sealed class HealthConditionState
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }

    public double HealthImpact { get; init; }

    public int? RemainingYears { get; set; }
}
