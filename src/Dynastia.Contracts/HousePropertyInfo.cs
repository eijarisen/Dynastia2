namespace Dynastia.Contracts;

public sealed record HousePropertyInfo(
    Guid Id,
    TownInfo Town,
    bool IsResidence,
    bool IsRented,
    Guid? AssignedHeirId = null,
    decimal PurchasePrice = 0m,
    int CapacityExtensions = 0,
    int BaseResidentCapacity = 6)
{
    public string Status =>
        IsResidence
            ? "Living In"
            : "Rented";

    public decimal ExtensionCost =>
        Math.Round(
            Math.Max(0m, PurchasePrice) * 0.25m,
            0,
            MidpointRounding.AwayFromZero);

    public int ResidentCapacity =>
        Math.Max(2, BaseResidentCapacity)
        + Math.Max(0, CapacityExtensions) * 2;

    public decimal ImprovementValue =>
        ExtensionCost * Math.Max(0, CapacityExtensions);
}
