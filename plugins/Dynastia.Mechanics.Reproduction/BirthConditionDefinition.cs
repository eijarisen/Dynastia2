using Dynastia.Contracts;

namespace Dynastia.Mechanics.Reproduction;

public sealed class BirthConditionDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    // Direct probability for this specific condition before the global scale.
    public double Probability { get; init; }

    public string HealthConditionId { get; init; } = string.Empty;
    public int StartYear { get; init; } = GameCalendarConfiguration.GameStartYear;
    public int? EndYear { get; init; }

    public Dictionary<string, int> StatModifiers { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
