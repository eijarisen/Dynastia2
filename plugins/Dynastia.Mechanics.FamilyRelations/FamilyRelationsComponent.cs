namespace Dynastia.Mechanics.FamilyRelations;

public sealed class FamilyRelationsComponent
{
    public List<FamilyRelationshipData> Relationships { get; set; } = [];
}

public sealed class FamilyRelationshipData
{
    public Guid PersonAId { get; set; }
    public Guid PersonBId { get; set; }
    public int Type { get; set; }
    public double Score { get; set; }
    public int CreatedYear { get; set; }
    public int LastMajorInteractionYear { get; set; }
}
