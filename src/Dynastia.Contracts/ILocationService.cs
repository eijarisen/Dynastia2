namespace Dynastia.Contracts;

public interface ILocationService
{
    LocationSnapshot GetLocation(
        IPerson person);

    TownInfo ChoosePropertyTown(
        IPerson householdHead);

    IReadOnlyList<TownInfo> GetTowns();

    TownInfo? FindTown(string townId);

    void SetPersonHomeTown(
        IPerson person,
        TownInfo town);

    void SetHouseholdHomeTown(
        IPerson householdHead,
        TownInfo town);
}
