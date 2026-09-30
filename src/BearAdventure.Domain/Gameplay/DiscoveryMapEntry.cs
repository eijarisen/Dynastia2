using BearAdventure.Domain.World;

namespace BearAdventure.Domain.Gameplay;

public sealed record DiscoveryMapEntry(
    int IslandId,
    BiomeType Biome,
    bool IsCurrent,
    bool LeftDock,
    bool RightDock,
    int ResidentCount,
    int WorkerCount,
    int BlockedWorkerCount,
    int PlayerChestCount,
    int StructureCount,
    int ReadyHoney,
    int FullHiveCount,
    int InsufficientHiveCount,
    bool CanTravel,
    BoatSide? ArrivalSide,
    string TravelReason);
