namespace Dynastia.Contracts;

public interface ITownEconomyService
{
    TownEconomyProfile GetProfile(TownInfo town);

    TownEconomyProfile? GetProfile(string townId);

    IReadOnlyList<TownEconomyProfile> GetAllProfiles();
}
