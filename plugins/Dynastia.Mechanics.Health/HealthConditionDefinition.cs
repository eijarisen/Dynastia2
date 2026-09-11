namespace Dynastia.Mechanics.Health;

public sealed class HealthConditionDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;

    public int? DurationMin { get; init; }
    public int? DurationMax { get; init; }

    public double HealthImpact { get; init; }
    public double Weight { get; init; }

    public bool RandomIllness { get; init; }

    public bool FamilyNews { get; init; }
}
