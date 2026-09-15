namespace Dynastia.StandardUI.Genealogy.Models;

public sealed record GenealogySnapshot(
    IReadOnlyList<GenealogyPersonRecord> People,
    Guid? FounderId,
    long TopologyVersion = 0,
    long VisualVersion = 0);

public sealed record GenealogyPersonRecord(
    Guid Id,
    string DisplayName,
    string FirstName,
    string Surname,
    string AvatarText,
    bool IsBloodline,
    bool IsMaleLineage,
    bool IsFemale,
    bool IsAlive,
    int BirthYear,
    int? DeathYear,
    double? HealthValue,
    string LifeSpanText,
    string TownText,
    string OccupationText,
    bool ShowOccupation,
    bool IsActiveHouseholdHead,
    string InfoTooltipText,
    IReadOnlyList<Guid> ParentIds,
    Guid? CurrentSpouseId,
    IReadOnlyList<GenealogyMarriageRecord> MarriageHistory);

public sealed record GenealogyMarriageRecord(
    Guid SpouseId,
    int StartYear,
    int? EndYear = null,
    string? EndReason = null);
