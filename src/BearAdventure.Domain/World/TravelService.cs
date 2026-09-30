using BearAdventure.Domain.Gameplay;

namespace BearAdventure.Domain.World;

public sealed record PreparedTravel(int OriginId, int DestinationId, BoatSide ArrivalSide,
    IslandDefinition Definition, IslandDeltaState State, WorldPoint Spawn);

public static class TravelService
{
    /// <summary>No discovery, inventory or source-state mutations during preparation.</summary>
    public static PreparedTravel Prepare(GameSessionState session, BoatSide departure, Func<int,IslandDefinition> generate)
    {
        if (!Enum.IsDefined(departure)) throw new ArgumentOutOfRangeException(nameof(departure));
        if (!session.Islands.TryGetValue(session.CurrentIslandId,out var origin) || !origin.IsBoatBuilt(departure))
            throw new InvalidOperationException("No departure boat exists.");
        int destination=checked(session.CurrentIslandId+(int)departure);
        var definition=session.Baselines.TryGetValue(destination,out var known) ? known : generate(destination);
        if (definition.IslandId!=destination) throw new InvalidOperationException("Destination identity mismatch.");
        var state=session.Islands.TryGetValue(destination,out var saved) ? saved : new IslandDeltaState();
        BoatSide arrival=departure==BoatSide.Left?BoatSide.Right:BoatSide.Left;
        var spawn=new WorldQueries(definition,state).SafeSpawn(arrival);
        return new(session.CurrentIslandId,destination,arrival,definition,state,spawn);
    }

    /// <summary>
    /// Prepare non-adjacent travel between already-discovered islands.
    /// The source and destination docks must already exist; preparation does not discover or mutate anything.
    /// </summary>
    public static PreparedTravel PrepareDirect(
        GameSessionState session,
        BoatSide departure,
        int destinationId,
        BoatSide arrival,
        Func<int,IslandDefinition> generate)
    {
        if(!Enum.IsDefined(departure)) throw new ArgumentOutOfRangeException(nameof(departure));
        if(!Enum.IsDefined(arrival)) throw new ArgumentOutOfRangeException(nameof(arrival));
        if(destinationId==session.CurrentIslandId) throw new InvalidOperationException("Already on that island.");
        if(!session.DiscoveredIslandIds.Contains(destinationId)) throw new InvalidOperationException("Destination is not discovered.");
        if(!session.Islands.TryGetValue(session.CurrentIslandId,out var origin) || !origin.IsBoatBuilt(departure))
            throw new InvalidOperationException("No departure boat exists.");

        var definition=session.Baselines.TryGetValue(destinationId,out var known) ? known : generate(destinationId);
        if(definition.IslandId!=destinationId) throw new InvalidOperationException("Destination identity mismatch.");

        var state=session.Islands.TryGetValue(destinationId,out var saved) ? saved : new IslandDeltaState();
        if(!state.IsBoatBuilt(arrival)) throw new InvalidOperationException("Destination dock is not built.");

        var spawn=new WorldQueries(definition,state).SafeSpawn(arrival);
        return new(session.CurrentIslandId,destinationId,arrival,definition,state,spawn);
    }

    public static void Commit(GameSessionState session, PreparedTravel travel)
    {
        if (session.CurrentIslandId!=travel.OriginId) throw new InvalidOperationException("Travel context is stale.");
        session.SetBaseline(travel.Definition);
        session.SetIslandState(travel.DestinationId,travel.State);
        travel.State.SetBoatBuilt(travel.ArrivalSide,true);
        session.CurrentIslandId=travel.DestinationId;
        session.PlayerLocation=new(travel.DestinationId,travel.Spawn.X,travel.Spawn.Y);
    }
}
