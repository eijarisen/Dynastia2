namespace Dynastia.Contracts;

public interface IHouseholdCapacityService
{
    HouseholdResidenceCapacitySnapshot GetResidenceCapacity(IPerson householdRepresentative);

    bool ExtendResidence(IPerson householdRepresentative, Guid propertyId);

    bool ExtendHouse(IPerson householdRepresentative, Guid propertyId);
}
