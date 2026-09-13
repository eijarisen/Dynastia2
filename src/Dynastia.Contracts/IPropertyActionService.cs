namespace Dynastia.Contracts;

public interface IPropertyActionService
{
    IReadOnlyList<PropertyTownOption> GetPurchaseOptions(IPerson householdHead);

    IReadOnlyList<PropertySaleOption> GetSaleOptions(IPerson householdHead);

    void PreparePurchase(IPerson householdHead, string townId);

    void PrepareSale(IPerson householdHead, Guid propertyId);

    bool CanMoveTo(IPerson householdHead, string townId);

    GameActionResult MoveHousehold(IPerson householdHead, string townId, int year);

    IReadOnlyList<HousePropertyInfo> GetTransferableProperties(IPerson householdRepresentative);

    GameActionResult TransferPropertyFromParent(
        IPerson parentRepresentative,
        IPerson receivingHouseholdHead,
        HousePropertyInfo property,
        int year);
}
