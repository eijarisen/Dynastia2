namespace Dynastia.Contracts;

public sealed record AdoptionPlacementInfo(
    AdoptionPlacementKind Kind,
    Guid? GuardianId,
    Guid? HouseholdHeadId,
    bool HasOrphanTrait,
    int? OrphanedYear,
    string Description);
