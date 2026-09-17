using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

[PersistedComponentId("crafts.person")]
public sealed class CraftComponent
{
    // Legacy compatibility field retained for older saves and external readers.
    public List<string> CraftIds { get; set; } = [];

    public string? InheritedCraftId { get; set; }

    public string? ChosenCraftId { get; set; }

    public List<string> ArchivedCraftIds { get; set; } = [];

    public string? ActiveCraftOccupationId { get; set; }

    // Legacy self-employment work-year storage. New mastery state lives in
    // ProgressByCraft, but this dictionary is kept synchronized for saves that
    // predate the vocation rework.
    public Dictionary<string, int> CraftWorkYearsByCraft { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, CraftProgressState> ProgressByCraft { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool VocationMigrationCompleted { get; set; }

    public decimal LastAnnualIncome { get; set; }

    public int LastIncomeYear { get; set; }
}

public sealed class CraftProgressState
{
    public string CraftId { get; set; } = string.Empty;

    public double ExperienceProgress { get; set; }

    public double EducationProgress { get; set; }

    public int SelfEmploymentYears { get; set; }

    public int CreditedPreLearningCareerYears { get; set; }

    public bool PriorCareerExperienceSeeded { get; set; }
}
