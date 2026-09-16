namespace Dynastia.Mechanics.Economy;

public sealed class FarmlandAssetState
{
    public Guid Id { get; set; }
    public string TownId { get; set; } = string.Empty;
    public int AcquiredYear { get; set; }
    public string AcquisitionSource { get; set; } = string.Empty;
}
