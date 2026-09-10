using Dynastia.Contracts;

namespace Dynastia.Mechanics.Adoption;

public sealed class AdoptionPlacementComponent
{
    public AdoptionPlacementKind Kind { get; set; }

    public Guid? GuardianId { get; set; }

    public Guid? HouseholdHeadId { get; set; }

    public int? OrphanedYear { get; set; }
}
