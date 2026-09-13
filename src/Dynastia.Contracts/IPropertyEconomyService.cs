namespace Dynastia.Contracts;

public interface IPropertyEconomyService
{
    TownInfo? GetResidenceTown(IPerson person);

    void SetResidenceTown(IPerson person, TownInfo town);

    bool HasHouseInTown(IPerson person, string townId);

    HousePropertyInfo? TakeHouse(IPerson person, Guid propertyId);
}
