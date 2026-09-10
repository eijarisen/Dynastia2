namespace Dynastia.Mechanics.Family;

public sealed class MarriageRecord
{
    public required Guid SpouseId { get; init; }
    public required int StartYear { get; init; }

    public int? EndYear { get; set; }
    public string? EndReason { get; set; }
}
