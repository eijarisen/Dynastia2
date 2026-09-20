namespace Dynastia.Mechanics.Heirlooms;

public sealed class HeirloomAssetState
{
    public Guid Id { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal AppraisedValue { get; set; }
    public int AcquiredYear { get; set; }
    public Guid? OriginPersonId { get; set; }
    public string OriginTriggerType { get; set; } = string.Empty;
    public string OriginTriggerId { get; set; } = string.Empty;
    public string OriginDescription { get; set; } = string.Empty;
    public Guid? AssignedHeirId { get; set; }
    public bool IsStolen { get; set; }
    public List<HeirloomOwnershipRecordState> OwnershipHistory { get; } = [];
}

public sealed class HeirloomOwnershipRecordState
{
    public int Year { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid? PersonId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
