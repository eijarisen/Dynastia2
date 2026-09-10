namespace Dynastia.Contracts;

public interface ILocationService
{
    LocationSnapshot GetLocation(
        IPerson person);

    TownInfo ChoosePropertyTown(
        IPerson householdHead);

    void SetHouseholdHomeTown(
        IPerson householdHead,
        TownInfo town);
}
