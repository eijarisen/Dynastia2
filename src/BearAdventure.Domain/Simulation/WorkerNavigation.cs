using BearAdventure.Domain.World;

namespace BearAdventure.Domain.Simulation;

public static class WorkerNavigation
{
    public const int MaxVisitedNodes = 16000;

    public static bool IsStandable(WorldQueries world, UndergroundCell cell)
    {
        if(!world.InBounds(cell) || world.Solid(cell)) return false;
        var head=new UndergroundCell(cell.CellX,cell.LogicalLevel+1);
        if(world.InBounds(head) && world.Solid(head)) return false;
        var below=new UndergroundCell(cell.CellX,cell.LogicalLevel-1);
        return world.Solid(below) || world.IsLadderCell(cell);
    }

    public static IReadOnlyDictionary<UndergroundCell,int> Distances(WorldQueries world, UndergroundCell from)
    {
        var result=new Dictionary<UndergroundCell,int>();
        if(!IsStandable(world,from)) return result;
        var q=new Queue<UndergroundCell>(); result[from]=0; q.Enqueue(from);
        while(q.Count>0 && result.Count<=MaxVisitedNodes)
        {
            var cell=q.Dequeue(); int distance=result[cell];
            foreach(var next in Neighbors(world,cell))
            {
                if(result.ContainsKey(next)) continue;
                result[next]=distance+1; q.Enqueue(next);
            }
        }
        return result;
    }

    public static bool TryPathLength(WorldQueries world, UndergroundCell from, UndergroundCell to, out int steps)
    {
        var distances=Distances(world,from);
        return distances.TryGetValue(to,out steps);
    }

    public static bool TryReachAdjacent(WorldQueries world, IReadOnlyDictionary<UndergroundCell,int> distances,
        UndergroundCell target, out UndergroundCell stand, out int steps)
    {
        stand=default; steps=int.MaxValue; bool found=false;
        foreach(var candidate in Adjacent(target))
        {
            if(!IsStandable(world,candidate) || !distances.TryGetValue(candidate,out int distance) || distance>=steps) continue;
            stand=candidate; steps=distance; found=true;
        }
        return found;
    }

    public static bool TryReachAdjacent(WorldQueries world, UndergroundCell from, UndergroundCell target,
        out UndergroundCell stand, out int steps) => TryReachAdjacent(world,Distances(world,from),target,out stand,out steps);

    private static IEnumerable<UndergroundCell> Adjacent(UndergroundCell target)
    {
        yield return new(target.CellX-1,target.LogicalLevel);
        yield return new(target.CellX+1,target.LogicalLevel);
        yield return new(target.CellX,target.LogicalLevel+1);
        yield return new(target.CellX,target.LogicalLevel-1);
    }

    private static IEnumerable<UndergroundCell> Neighbors(WorldQueries world, UndergroundCell cell)
    {
        foreach(int dx in new[]{-1,1})
        {
            int x=cell.CellX+dx;
            foreach(int dl in new[]{0,1,-1})
            {
                var candidate=new UndergroundCell(x,cell.LogicalLevel+dl);
                if(IsStandable(world,candidate)) { yield return candidate; break; }
            }
        }
        if(world.IsLadderCell(cell))
        {
            var up=new UndergroundCell(cell.CellX,cell.LogicalLevel+1);
            var down=new UndergroundCell(cell.CellX,cell.LogicalLevel-1);
            if(IsStandable(world,up)) yield return up;
            if(IsStandable(world,down)) yield return down;
        }
    }
}
