namespace Dynastia.Mechanics.Economy;

public static class HouseExtensionRules
{
    public const int BaseResidenceCapacity = 8;
    public const int CapacityPerExtension = 2;
    public const decimal ExtensionPriceFraction = 0.25m;

    public static int GetResidentCapacity(int extensionCount) =>
        BaseResidenceCapacity
        + Math.Max(0, extensionCount) * CapacityPerExtension;

    public static decimal GetExtensionCost(decimal purchasePrice) =>
        Math.Round(
            Math.Max(0m, purchasePrice) * ExtensionPriceFraction,
            0,
            MidpointRounding.AwayFromZero);
}
