namespace Dynastia.StandardUI.Map.Models;

public sealed record TownMapResident(
    Guid PersonId,
    string DisplayName,
    bool IsPlayableHouseholdHead,
    bool IsActiveHouseholdHead);

public sealed record TownMapItem(
    string TownId,
    string Name,
    string County,
    int Population,
    double ProjectedX,
    double ProjectedY,
    IReadOnlyList<TownMapResident> DynastyResidents,
    int PlayableHouseholds,
    int OwnedHouses,
    bool IsCurrentHouseholdTown)
{
    public string DisplayName =>
        string.Equals(
            Name,
            County,
            StringComparison.OrdinalIgnoreCase)
                ? Name
                : $"{Name}, {County}";

    public bool HasDynastyResidents =>
        DynastyResidents.Count > 0;

    public bool HasPlayableHousehold =>
        PlayableHouseholds > 0;

    public bool HasOwnedHouse =>
        OwnedHouses > 0;
}

public sealed record TownMapSnapshot(
    IReadOnlyList<TownMapItem> Towns,
    double MinX,
    double MaxX,
    double MinY,
    double MaxY,
    string? CurrentHouseholdTownId);
