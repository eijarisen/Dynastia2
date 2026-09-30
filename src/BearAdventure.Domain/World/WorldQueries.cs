using BearAdventure.Domain.Gameplay;

namespace BearAdventure.Domain.World;

/// <summary>One spatial model for physics, building, growth, structures and interaction. All coordinates use WorldGrid.</summary>
public sealed class WorldQueries
{
    private readonly IReadOnlyList<GeneratedStructureLayout> _structures;

    public WorldQueries(IslandDefinition definition, IslandDeltaState state)
    {
        Definition = definition;
        State = state;
        _structures = GeneratedStructureLayoutService.BuildAll(definition);
    }

    public IslandDefinition Definition { get; }
    public IslandDeltaState State { get; }
    public IReadOnlyList<GeneratedStructureLayout> StructureLayouts => _structures;

    public bool InBounds(UndergroundCell c) => WorldGrid.IsInBounds(Definition, c);

    public GeneratedStructureLayout? StructureContaining(UndergroundCell c) =>
        _structures.FirstOrDefault(s => s.Solid(c) || s.Carves(c) || s.Access(c) || s.Ladder(c));

    public bool GeneratedStructureSolid(UndergroundCell c) => _structures.Any(s => s.Solid(c));
    public bool GeneratedStructureCarves(UndergroundCell c) => _structures.Any(s => s.Carves(c));
    public bool GeneratedStructureLadder(UndergroundCell c) => _structures.Any(s => s.Ladder(c));
    public bool GeneratedStructureAccess(UndergroundCell c) => _structures.Any(s => s.Access(c));

    public bool TerrainSolid(UndergroundCell c) => InBounds(c)
        && c.LogicalLevel <= Definition.SurfaceLevels[c.CellX]
        && !GeneratedStructureCarves(c)
        && !Definition.UndergroundOpenCells.Contains(c)
        && !State.IsUndergroundCellMined(c);

    public bool Solid(UndergroundCell c, int? ignorePlacement = null) =>
        (c.CellX >= 0 && c.CellX < Definition.WidthCells &&
         c.LogicalLevel == IslandGenerationSettings.DeepestLogicalLevel - 1)
        || GeneratedStructureSolid(c)
        || TerrainSolid(c)
        || State.PlacedObjects.Any(p =>
            p.PlacementId != ignorePlacement
            && p.CellX == c.CellX
            && p.LogicalLevel == c.LogicalLevel
            && PlacementRules.IsSolid(p.Item, p.Layer));

    public bool Exposed(UndergroundCell c) => Neighbors(c).Any(n => !Solid(n));

    public static IEnumerable<UndergroundCell> Neighbors(UndergroundCell c)
    {
        yield return new(c.CellX - 1, c.LogicalLevel);
        yield return new(c.CellX + 1, c.LogicalLevel);
        yield return new(c.CellX, c.LogicalLevel - 1);
        yield return new(c.CellX, c.LogicalLevel + 1);
    }

    public bool IsShore(int x) => x < WorldGrid.ShoreCells || x >= Definition.WidthCells - WorldGrid.ShoreCells;

    /// <summary>Mine-ladder columns only. Use IsLadderCell for actual movement.</summary>
    public bool IsLadderColumn(int x) =>
        x >= Definition.MineShaftLeftCell && x < Definition.MineShaftLeftCell + Definition.MineShaftWidthCells;

    public bool IsLadderCell(UndergroundCell cell)
    {
        if (!InBounds(cell)) return false;
        if (GeneratedStructureLadder(cell)) return true;
        if (!IsLadderColumn(cell.CellX)) return false;

        int highest = Enumerable.Range(Definition.MineShaftLeftCell, Definition.MineShaftWidthCells)
            .Max(x => Definition.SurfaceLevels[x]);

        return cell.LogicalLevel <= highest + 1
            && cell.LogicalLevel >= IslandGenerationSettings.DeepestLogicalLevel;
    }

    public bool IsLadder(WorldPoint p)
    {
        // Use the point itself plus the feet cell to avoid flicker at exact cell boundaries.
        UndergroundCell center = WorldGrid.CellAt(p);
        UndergroundCell feet = WorldGrid.CellAt(new WorldPoint(p.X, p.Y + 28));
        if (IsLadderCell(center) || IsLadderCell(feet)) return true;

        int highest = Enumerable.Range(Definition.MineShaftLeftCell, Definition.MineShaftWidthCells)
            .Max(x => Definition.SurfaceLevels[x]);
        double left = WorldGrid.LandLeft + Definition.MineShaftLeftCell * WorldGrid.CellSize;

        return p.X >= left
            && p.X < left + Definition.MineShaftWidthCells * WorldGrid.CellSize
            && p.Y >= -highest * WorldGrid.CellSize - WorldGrid.CellSize
            && p.Y < -(IslandGenerationSettings.DeepestLogicalLevel - 1) * WorldGrid.CellSize;
    }

    public bool OnBoat(WorldRect bear) => Enum.GetValues<BoatSide>().Any(side =>
        State.IsBoatBuilt(side)
        && bear.Right > WorldGrid.BoatHull(Definition.WidthCells, side).X
        && bear.X < WorldGrid.BoatHull(Definition.WidthCells, side).Right
        && Math.Abs(bear.Bottom - WorldGrid.BoatHull(Definition.WidthCells, side).Y) <= 4);

    public bool FreeForBear(WorldPoint p)
    {
        if (!double.IsFinite(p.X) || !double.IsFinite(p.Y)) return false;

        WorldRect r = WorldGrid.BearRect(p);
        if (r.X < 0
            || r.Right > (Definition.WidthCells + WorldGrid.WaterMarginCells * 2) * WorldGrid.CellSize
            || r.Y < -IslandGenerationSettings.HighestLogicalLevel * WorldGrid.CellSize
            || r.Bottom > -(IslandGenerationSettings.DeepestLogicalLevel - 1) * WorldGrid.CellSize)
            return false;

        if ((p.X < WorldGrid.LandLeft
             || p.X >= WorldGrid.LandLeft + Definition.WidthCells * WorldGrid.CellSize)
            && p.Y > 25
            && !OnBoat(r))
            return false;

        UndergroundCell a = WorldGrid.CellAt(new WorldPoint(r.X + 0.001, r.Y + 0.001));
        UndergroundCell b = WorldGrid.CellAt(new WorldPoint(r.Right - 0.001, r.Bottom - 0.001));

        for (int x = a.CellX; x <= b.CellX; x++)
            for (int l = b.LogicalLevel; l <= a.LogicalLevel; l++)
                if (Solid(new UndergroundCell(x, l))) return false;

        foreach (BoatSide side in Enum.GetValues<BoatSide>())
            if (State.IsBoatBuilt(side) && r.Intersects(WorldGrid.BoatHull(Definition.WidthCells, side)))
                return false;

        return true;
    }

    public WorldPoint SafeSpawn(BoatSide? side = null)
    {
        int preferred = side == BoatSide.Right
            ? Definition.WidthCells - 2
            : side == BoatSide.Left
                ? 1
                : Definition.SuggestedSpawnCell;

        foreach (int x in Enumerable.Range(0, Definition.WidthCells).OrderBy(x => Math.Abs(x - preferred)))
        {
            if (IsLadderColumn(x)) continue;

            for (int l = IslandGenerationSettings.HighestLogicalLevel - 2;
                 l >= IslandGenerationSettings.DeepestLogicalLevel + 1;
                 l--)
            {
                if (!Solid(new UndergroundCell(x, l))) continue;

                var center = new WorldPoint(
                    WorldGrid.LandLeft + (x + 0.5) * WorldGrid.CellSize,
                    -l * WorldGrid.CellSize - 30);

                if (FreeForBear(center)) return center;
            }
        }

        throw new InvalidOperationException("No safe arrival point exists. Origin was not changed.");
    }

    public bool Fertile(UndergroundCell plant)
    {
        if (!InBounds(plant) || plant.LogicalLevel <= 0) return false;

        var below = new UndergroundCell(plant.CellX, plant.LogicalLevel - 1);
        return TerrainSolid(below) && below.LogicalLevel == Definition.SurfaceLevels[plant.CellX];
    }

    public static NaturalFeatureKind PlantedKind(BiomeType biome) => biome switch
    {
        BiomeType.Desert => NaturalFeatureKind.Cactus,
        BiomeType.Snowy => NaturalFeatureKind.Pine,
        BiomeType.Jungle => NaturalFeatureKind.Palm,
        _ => NaturalFeatureKind.Tree,
    };

    public WorldRect PlantFootprint(int x, int level, NaturalFeatureKind kind)
    {
        WorldRect cell = WorldGrid.CellRect(new UndergroundCell(x, level));
        bool tree = kind is NaturalFeatureKind.Tree or NaturalFeatureKind.Pine or NaturalFeatureKind.Palm;
        int h = tree ? 3 : kind == NaturalFeatureKind.Cactus ? 2 : 1;

        return new WorldRect(
            cell.X - (tree ? WorldGrid.CellSize : 0),
            cell.Y - (h - 1) * WorldGrid.CellSize,
            (tree ? 3 : 1) * WorldGrid.CellSize,
            h * WorldGrid.CellSize);
    }

    public WorldRect Footprint(PlacedObjectState p) => PlacementRules.IsPlantable(p.Item)
        ? PlantFootprint(
            p.CellX,
            p.LogicalLevel,
            p.Item == ItemType.Sapling
                ? p.PlantKind
                : p.Item == ItemType.Grass
                    ? NaturalFeatureKind.Grass
                    : NaturalFeatureKind.Flower)
        : WorldGrid.CellRect(new UndergroundCell(p.CellX, p.LogicalLevel));

    public bool StructureIntersects(WorldRect r) => _structures.Any(s => s.ReservedBounds.Intersects(r));

    private bool FootprintClear(WorldRect footprint, int? ignorePlaced, bool natural)
    {
        UndergroundCell a = WorldGrid.CellAt(new WorldPoint(footprint.X + 0.001, footprint.Y + 0.001));
        UndergroundCell b = WorldGrid.CellAt(new WorldPoint(footprint.Right - 0.001, footprint.Bottom - 0.001));

        if (!InBounds(a) || !InBounds(b)) return false;

        for (int x = a.CellX; x <= b.CellX; x++)
        {
            if (IsShore(x) || IsLadderColumn(x)) return false;

            for (int l = b.LogicalLevel; l <= a.LogicalLevel; l++)
                if (TerrainSolid(new UndergroundCell(x, l))) return false;
        }

        if (StructureIntersects(footprint)) return false;

        foreach (PlacedObjectState placed in State.PlacedObjects)
        {
            if (placed.PlacementId == ignorePlaced || placed.Layer == BuildLayer.Background) continue;
            if (footprint.Intersects(Footprint(placed))) return false;
        }

        return true;
    }

    public bool CanRegrow(NaturalFeatureSpawn f)
    {
        var anchor = new UndergroundCell(f.CellX, f.SurfaceLevel + 1);
        bool flora = f.Kind is not NaturalFeatureKind.Rock;

        if (flora ? !Fertile(anchor) : !TerrainSolid(new UndergroundCell(f.CellX, f.SurfaceLevel)))
            return false;

        return FootprintClear(PlantFootprint(f.CellX, f.SurfaceLevel + 1, f.Kind), null, true);
    }

    public bool NaturalPresent(NaturalFeatureSpawn f) =>
        !State.IsNaturalFeatureHarvested(f.FeatureId) && CanRegrow(f);

    public bool PlantCanGrow(PlacedObjectState p) =>
        Fertile(new UndergroundCell(p.CellX, p.LogicalLevel))
        && FootprintClear(Footprint(p), p.PlacementId, false);

    public bool PlantedFlowerValid(PlacedObjectState p) =>
        PlacementRules.IsFlower(p.Item) && PlantCanGrow(p);

    public bool CanPlace(
        ItemType item,
        UndergroundCell cell,
        BuildLayer layer,
        WorldRect bear,
        out string reason)
    {
        reason = string.Empty;

        if (!Enum.IsDefined(layer) || !PlacementRules.IsPlaceable(item))
        {
            reason = "That item cannot be placed.";
            return false;
        }

        if (!InBounds(cell))
        {
            reason = "Outside the +100 to -100 build range.";
            return false;
        }

        if (!PlacementRules.IsBlock(item) && layer != BuildLayer.Solid)
        {
            reason = "Objects belong in the foreground.";
            return false;
        }

        if (IsShore(cell.CellX))
        {
            reason = "Keep the shoreline and its boat access clear.";
            return false;
        }

        if (IsLadderColumn(cell.CellX) || GeneratedStructureLadder(cell))
        {
            reason = "Keep ladders clear.";
            return false;
        }

        if (GeneratedStructureAccess(cell))
        {
            reason = "Keep the generated building entrance clear.";
            return false;
        }

        if (GeneratedStructureSolid(cell))
        {
            reason = "A generated structure occupies that cell.";
            return false;
        }

        if (TerrainSolid(cell))
        {
            reason = "Mine that terrain cell first.";
            return false;
        }

        bool background = PlacementRules.IsBlock(item) && layer == BuildLayer.Background;

        if (State.PlacedObjects.Any(p =>
            p.CellX == cell.CellX
            && p.LogicalLevel == cell.LogicalLevel
            && (background ? p.Layer == BuildLayer.Background : p.Layer != BuildLayer.Background)))
        {
            reason = "That layer is occupied.";
            return false;
        }

        WorldRect rect = WorldGrid.CellRect(cell);

        if (Definition.GeneratedStructures.Any(s =>
            rect.Intersects(WorldGrid.CellRect(new UndergroundCell(s.Chest.CellX, s.Chest.LogicalLevel)))))
        {
            reason = "A generated chest occupies that space.";
            return false;
        }

        if (!background
            && State.PlacedObjects
                .Where(p => PlacementRules.IsPlantable(p.Item))
                .Any(p => rect.Intersects(Footprint(p))))
        {
            reason = "Leave room for the planted vegetation to mature.";
            return false;
        }

        if (PlacementRules.IsSolid(item, layer) && bear.Intersects(rect))
        {
            reason = "Cannot place a solid object on the bear.";
            return false;
        }

        if (PlacementRules.IsPlantable(item))
        {
            if (!Fertile(cell))
            {
                reason = "Plant on undisturbed natural surface soil, not cave rock or generated flooring.";
                return false;
            }

            NaturalFeatureKind kind = item == ItemType.Sapling
                ? PlantedKind(Definition.Biome)
                : item == ItemType.Grass
                    ? NaturalFeatureKind.Grass
                    : NaturalFeatureKind.Flower;

            WorldRect footprint = PlantFootprint(cell.CellX, cell.LogicalLevel, kind);

            if (!FootprintClear(footprint, null, false))
            {
                reason = "Not enough clear space for the mature plant.";
                return false;
            }

            if (Definition.NaturalFeatures.Any(f =>
                NaturalPresent(f)
                && footprint.Intersects(PlantFootprint(f.CellX, f.SurfaceLevel + 1, f.Kind))))
            {
                reason = "Clear the existing vegetation before planting here.";
                return false;
            }
        }
        else if (!background
                 && Definition.NaturalFeatures.Any(f =>
                     NaturalPresent(f)
                     && rect.Intersects(PlantFootprint(f.CellX, f.SurfaceLevel + 1, f.Kind))))
        {
            reason = "Harvest the resource in that space first.";
            return false;
        }

        bool support = Solid(new UndergroundCell(cell.CellX, cell.LogicalLevel - 1));

        if (PlacementRules.IsBlock(item))
        {
            support |= Neighbors(cell).Any(TerrainSolid);
            support |= State.PlacedObjects.Any(p =>
                PlacementRules.IsBlock(p.Item)
                && (background || p.Layer == BuildLayer.Solid)
                && Math.Abs(p.CellX - cell.CellX) + Math.Abs(p.LogicalLevel - cell.LogicalLevel) == 1);
        }

        if (!support)
        {
            reason = "Needs current terrain, generated flooring or a supported block. Background walls do not support solid objects.";
            return false;
        }

        return true;
    }

    public bool CanMine(UndergroundCell c, out string reason)
    {
        reason = string.Empty;

        if (GeneratedStructureSolid(c))
        {
            reason = "Generated structures are permanent in this version.";
            return false;
        }

        if (!TerrainSolid(c) || !Exposed(c))
        {
            reason = "Aim at an exposed terrain cell.";
            return false;
        }

        if (IsShore(c.CellX))
        {
            reason = "The shoreline foundation protects boat access.";
            return false;
        }

        if (_structures.Any(s => s.SolidCells.Contains(new UndergroundCell(c.CellX, c.LogicalLevel + 1))))
        {
            reason = "This tile supports a generated structure.";
            return false;
        }

        if (State.PlacedObjects.Any(p =>
            p.Layer != BuildLayer.Background
            && p.CellX == c.CellX
            && p.LogicalLevel == c.LogicalLevel + 1))
        {
            reason = "Move or remove the object supported by this tile first.";
            return false;
        }

        if (Definition.GeneratedStructures.Any(s =>
            s.Chest.CellX == c.CellX && s.Chest.LogicalLevel == c.LogicalLevel + 1))
        {
            reason = "This tile supports a generated chest.";
            return false;
        }

        if (Definition.NaturalFeatures.Any(f =>
            f.CellX == c.CellX
            && f.SurfaceLevel == c.LogicalLevel
            && NaturalPresent(f)))
        {
            reason = "Harvest the surface resource before mining its ground.";
            return false;
        }

        return true;
    }

    public bool CanRemove(PlacedObjectState p, out string reason)
    {
        reason = string.Empty;

        if (!p.Contents.IsEmpty)
        {
            reason = "Empty this chest before dismantling it.";
            return false;
        }

        if (PlacementRules.IsSolid(p.Item, p.Layer)
            && State.PlacedObjects.Any(other =>
                other.PlacementId != p.PlacementId
                && other.Layer != BuildLayer.Background
                && other.CellX == p.CellX
                && other.LogicalLevel == p.LogicalLevel + 1))
        {
            reason = "Remove the object resting on this block first.";
            return false;
        }

        return true;
    }

    public bool TryEntity(
        WorldEntityRef id,
        out PlacedObjectState? placed,
        out GeneratedChestDefinition? chest,
        out WorldPoint center)
    {
        placed = null;
        chest = null;
        center = default;

        if (id.IslandId != Definition.IslandId || !Enum.IsDefined(id.Kind))
            return false;

        if (id.Kind == WorldEntityKind.Placed)
            placed = State.PlacedObjects.FirstOrDefault(p => p.PlacementId == id.EntityId);
        else
            chest = Definition.GeneratedStructures
                .Select(s => s.Chest)
                .FirstOrDefault(c => c.ChestId == id.EntityId);

        if (placed is null && chest is null) return false;

        center = placed is not null
            ? WorldGrid.Center(placed.CellX, placed.LogicalLevel)
            : WorldGrid.Center(chest!.CellX, chest.LogicalLevel);

        return true;
    }

    public bool InReach(WorldPoint from, WorldPoint to, double range) =>
        from.DistanceSquared(to) <= range * range;
}
