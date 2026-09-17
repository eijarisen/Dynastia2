using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

[PersistedComponentId("family_relations.person")]
public sealed class FamilyRelationsComponent
{
    public List<FamilyRelationshipData> Relationships { get; set; } = [];
}

public sealed class FamilyRelationshipData
{
    public Guid PersonAId { get; set; }
    public Guid PersonBId { get; set; }
    public int Type { get; set; }

    // Legacy single-axis score is retained for save compatibility and as a
    // composite summary for older consumers. New logic uses the two axes.
    public double Score { get; set; }
    public double Familiarity { get; set; } = -1;
    public double Sympathy { get; set; } = -1;

    public int CreatedYear { get; set; }
    public int LastMajorInteractionYear { get; set; }
}
