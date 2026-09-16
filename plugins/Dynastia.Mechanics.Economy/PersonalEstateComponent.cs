namespace Dynastia.Mechanics.Economy;

public sealed class PersonalEstateComponent
{
    public decimal PendingInheritance { get; set; }

    // Legacy integer retained so older saves remain loadable.
    public int PendingHouses { get; set; }

    public List<HousePropertyState> PendingHouseProperties { get; } = [];

    public List<FarmlandAssetState> PendingFarmland { get; } = [];
}
