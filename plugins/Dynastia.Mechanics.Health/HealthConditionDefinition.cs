using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class HealthConditionDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    // Legacy presentation/persistence type used by existing UI and mortality.
    public string Type { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;
    public string Course { get; init; } = string.Empty;
    public int MinimumAge { get; init; }
    public int? MaximumAge { get; init; }
    public int StartYear { get; init; } = GameCalendarConfiguration.GameStartYear;
    public int? EndYear { get; init; }
    public string? GeneticTag { get; init; }
    public bool Newsworthy { get; init; }

    public int? DurationMin { get; init; }
    public int? DurationMax { get; init; }

    public double HealthImpact { get; init; }
    public double ImmediateHealthImpact { get; init; }
    public double Weight { get; init; }

    // Kept for backward-compatible data loading. The reworked generator uses Category.
    public bool RandomIllness { get; init; }
    public bool FamilyNews { get; init; }
}
