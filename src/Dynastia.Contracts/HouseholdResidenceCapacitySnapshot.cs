namespace Dynastia.Contracts;

public sealed record HouseholdResidenceCapacitySnapshot(
    Guid? PropertyId,
    bool OwnsResidence,
    int BaseCapacity,
    int ExtensionCount,
    int ResidentCapacity,
    decimal PurchasePrice,
    decimal ExtensionCost);
