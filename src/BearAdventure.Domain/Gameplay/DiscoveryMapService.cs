using BearAdventure.Domain.Simulation;
using BearAdventure.Domain.World;

namespace BearAdventure.Domain.Gameplay;

public static class DiscoveryMapService
{
    public static IReadOnlyList<DiscoveryMapEntry> Build(
        GameSessionState session,
        IslandGenerator generator,
        BoatSide? sourceDock)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(generator);

        var result = new List<DiscoveryMapEntry>();

        foreach (int islandId in session.DiscoveredIslandIds.OrderBy(id => id))
        {
            IslandDefinition definition = session.GetDefinition(islandId, generator);
            IslandDeltaState state = session.Islands.TryGetValue(islandId,out var savedState) ? savedState : new IslandDeltaState();
            var world = new WorldQueries(definition, state);

            ResidentState[] residents = session.Residents
                .Where(r => r.Status == ResidentStatus.World && r.IslandId == islandId)
                .ToArray();

            int workers = residents.Count(r => r.Worker?.Assigned == true);
            int blockedWorkers = residents.Count(
                r => r.Worker is { Assigned: true, Phase: WorkerPhase.Blocked });

            var hives = state.PlacedObjects
                .Where(p => p.Item == ItemType.Beehive)
                .OrderBy(p => p.PlacementId)
                .ToArray();

            int readyHoney = hives.Sum(h => h.StoredOutput);
            int full = 0;
            int insufficient = 0;

            foreach (PlacedObjectState hive in hives)
            {
                HiveInfo info = WorldSimulationService.Inspect(world, hive);
                if (info.Status == HiveStatus.Full) full++;
                if (info.Status == HiveStatus.InsufficientFlowers) insufficient++;
            }

            bool current = islandId == session.CurrentIslandId;
            BoatSide? arrival = current ? null : ChooseArrival(session.CurrentIslandId, islandId, state);
            bool canTravel = !current && sourceDock.HasValue && arrival.HasValue;

            string reason = current
                ? "You are here."
                : !sourceDock.HasValue
                    ? "Stand beside a completed dock to travel."
                    : !arrival.HasValue
                        ? "Destination has no completed dock."
                        : $"Travel from {sourceDock.Value.ToString().ToLowerInvariant()} dock to {arrival.Value.ToString().ToLowerInvariant()} dock.";

            result.Add(new DiscoveryMapEntry(
                islandId,
                definition.Biome,
                current,
                state.LeftBoatBuilt,
                state.RightBoatBuilt,
                residents.Length,
                workers,
                blockedWorkers,
                state.PlacedObjects.Count(p => p.Item == ItemType.Chest),
                definition.GeneratedStructures.Count,
                readyHoney,
                full,
                insufficient,
                canTravel,
                arrival,
                reason));
        }

        return result;
    }

    public static BoatSide? ChooseArrival(
        int currentIslandId,
        int targetIslandId,
        IslandDeltaState target)
    {
        if (targetIslandId == currentIslandId)
            return null;

        BoatSide preferred = targetIslandId > currentIslandId
            ? BoatSide.Left
            : BoatSide.Right;

        if (target.IsBoatBuilt(preferred))
            return preferred;

        BoatSide fallback = preferred == BoatSide.Left
            ? BoatSide.Right
            : BoatSide.Left;

        return target.IsBoatBuilt(fallback)
            ? fallback
            : null;
    }
}
