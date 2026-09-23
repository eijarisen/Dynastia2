using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

[PersistedComponentId("economy.personal_estate")]
public sealed class PersonalEstateComponent
{
    // Signed pending estate balance: positive = inheritance, negative = inherited debt.
    public decimal PendingInheritance { get; set; }

    // Legacy integer retained so older saves remain loadable.
    public int PendingHouses { get; set; }

    public List<HousePropertyState> PendingHouseProperties { get; } = [];

    public List<FarmlandAssetState> PendingFarmland { get; } = [];
}
