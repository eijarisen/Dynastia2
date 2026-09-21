namespace Dynastia.Contracts;

public interface ILocationService
{
    LocationSnapshot GetLocation(
        IPerson person);

    TownInfo ChoosePropertyTown(
        IPerson householdHead);

    IReadOnlyList<TownInfo> GetTowns();

    TownInfo? FindTown(string townId);

    TownInfo? FindTownAtYear(string townId, int year) =>
        FindTown(townId);

    string GetBirthplaceDisplayName(IPerson person) =>
        GetLocation(person).Birthplace.DisplayName;

    void SetForeignBirthplace(
        IPerson person,
        string? city,
        string? country)
    {
    }

    void SetPersonHomeTown(
        IPerson person,
        TownInfo town);

    void SetHouseholdHomeTown(
        IPerson householdHead,
        TownInfo town);
}
