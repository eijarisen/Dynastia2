namespace Dynastia.Contracts;

public interface ITownDirectoryService
{
    IReadOnlyList<TownInfo> GetAllTowns();

    TownInfo? FindTown(string townId);
}
