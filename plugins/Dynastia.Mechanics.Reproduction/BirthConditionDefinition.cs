namespace Dynastia.Mechanics.Reproduction;

public sealed class BirthConditionDefinition
{
    public string Id { get; init; } =
        string.Empty;

    public string Name { get; init; } =
        string.Empty;

    // Direct probability for this specific condition.
    // Example: 0.0015 = 0.15%.
    public double Probability { get; init; }

    public string HealthConditionId { get; init; } =
        string.Empty;

    public Dictionary<string, int> StatModifiers { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
