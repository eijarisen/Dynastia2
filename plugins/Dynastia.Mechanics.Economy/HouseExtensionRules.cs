namespace Dynastia.Mechanics.Economy;

public static class HouseExtensionRules
{
    public const int BaseResidenceCapacity = 6;
    public const int CapacityPerExtension = 2;
    public const decimal ExtensionPriceFraction = 0.25m;

    public static int GetResidentCapacity(int extensionCount) =>
        GetResidentCapacity(BaseResidenceCapacity, extensionCount);

    public static int GetResidentCapacity(
        int baseResidentCapacity,
        int extensionCount) =>
        Math.Max(2, baseResidentCapacity)
        + Math.Max(0, extensionCount) * CapacityPerExtension;

    public static decimal GetExtensionCost(decimal purchasePrice) =>
        Math.Round(
            Math.Max(0m, purchasePrice) * ExtensionPriceFraction,
            0,
            MidpointRounding.AwayFromZero);
}
