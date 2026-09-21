namespace Dynastia.Contracts;

public interface ILocalServiceTownResolver
{
    TownInfo Resolve(TownInfo town);

    TownInfo Resolve(string placeId, int year);

    IReadOnlyList<string> GetClusterPlaceIds(string placeId);
}
