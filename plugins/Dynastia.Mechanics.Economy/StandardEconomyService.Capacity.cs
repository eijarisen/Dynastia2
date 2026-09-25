using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    public int GetDefaultResidenceCapacity(TownInfo town) =>
        HouseExtensionRules.BaseResidenceCapacity;

    public HouseholdResidenceCapacitySnapshot GetResidenceCapacity(
        IPerson householdRepresentative)
    {
        var resolved = FindHousehold(householdRepresentative);
        if (resolved is null)
        {
            return new HouseholdResidenceCapacitySnapshot(
                null,
                false,
                HouseExtensionRules.BaseResidenceCapacity,
                0,
                HouseExtensionRules.BaseResidenceCapacity,
                0m,
                0m);
        }

        var household = resolved.Value.Household;
        var head = GetHead(household);
        SynchronizeHouses(head, household);

        var residence = household.Houses
            .Select((house, index) => new
            {
                State = house,
                Info = ToInfo(house, index, head, household)
            })
            .FirstOrDefault(item => item.Info.IsResidence);

        if (residence is null)
        {
            return new HouseholdResidenceCapacitySnapshot(
                null,
                false,
                HouseExtensionRules.BaseResidenceCapacity,
                0,
                HouseExtensionRules.BaseResidenceCapacity,
                0m,
                0m);
        }

        var purchasePrice = residence.State.PurchasePrice > 0m
            ? residence.State.PurchasePrice
            : GetHousePrice(residence.Info.Town);
        var extensions = Math.Max(0, residence.State.CapacityExtensions);

        var baseCapacity = residence.State.BaseResidentCapacity > 0
            ? residence.State.BaseResidentCapacity
            : HouseExtensionRules.BaseResidenceCapacity;

        return new HouseholdResidenceCapacitySnapshot(
            residence.State.Id,
            true,
            baseCapacity,
            extensions,
            HouseExtensionRules.GetResidentCapacity(baseCapacity, extensions),
            purchasePrice,
            HouseExtensionRules.GetExtensionCost(purchasePrice));
    }


    public bool ExtendHouse(
        IPerson householdRepresentative,
        Guid propertyId)
    {
        var resolved = FindHousehold(householdRepresentative);
        if (resolved is null)
            return false;

        var household = resolved.Value.Household;
        var head = GetHead(household);
        SynchronizeHouses(head, household);

        var house = household.Houses
            .FirstOrDefault(candidate => candidate.Id == propertyId);

        if (house is null)
            return false;

        house.CapacityExtensions =
            Math.Max(0, house.CapacityExtensions) + 1;
        return true;
    }

    public bool ExtendResidence(
        IPerson householdRepresentative,
        Guid propertyId)
    {
        var resolved = FindHousehold(householdRepresentative);
        if (resolved is null)
            return false;

        var household = resolved.Value.Household;
        var head = GetHead(household);
        SynchronizeHouses(head, household);

        var residence = household.Houses
            .Select((house, index) => new
            {
                State = house,
                Info = ToInfo(house, index, head, household)
            })
            .FirstOrDefault(item => item.Info.IsResidence);

        if (residence is null || residence.State.Id != propertyId)
            return false;

        residence.State.CapacityExtensions =
            Math.Max(0, residence.State.CapacityExtensions) + 1;
        return true;
    }
}
