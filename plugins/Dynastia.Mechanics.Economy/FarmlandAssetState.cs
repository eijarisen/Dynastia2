namespace Dynastia.Mechanics.Economy;

public sealed class FarmlandAssetState
{
    public Guid Id { get; set; }
    public string TownId { get; set; } = string.Empty;
    public int AcquiredYear { get; set; }
    public string AcquisitionSource { get; set; } = string.Empty;
    public Guid? AssignedHeirId { get; set; }
    public string FarmTypeId { get; set; } = string.Empty;
    public string? LivestockTypeId { get; set; }
}
