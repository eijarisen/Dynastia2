using BearAdventure.Domain.Gameplay;
using BearAdventure.Domain.Simulation;
using BearAdventure.Domain.World;
using BearAdventure.Rendering;
using BearAdventure.Rendering.PixelArt;
using Godot;

namespace BearAdventure.World;

/// <summary>Godot projection only. Rule decisions and persistent mutations live in the domain.</summary>
public partial class IslandView : Node2D
{
    public const float CellSize = (float)WorldGrid.CellSize;
    public const int WaterMarginCells = WorldGrid.WaterMarginCells;
    public const int BoatConstructionShoreCells = WorldGrid.ShoreCells;
    private readonly IslandDefinition _island;
    private readonly IslandDeltaState _state;
    private readonly BiomePalette _palette;
    private readonly IReadOnlyList<ResidentState> _residents;
    private int? _highlightedFeatureId, _highlightedPlacementId;
    private float _highlightedFeatureProgress, _highlightedPlacementProgress;
    private UndergroundCell? _highlightedMineCell;
    private float _highlightedMineProgress;
    private ItemType? _previewItem;
    private int _previewCellX, _previewLogicalLevel;
    private BuildLayer _previewLayer;
    private bool _previewValid;
    private Rect2? _entityHighlight;
    private int? _residentHighlight;
    private StaticBody2D? _terrainCollisionBody, _placedCollisionBody, _boatCollisionBody, _structureCollisionBody;
    private readonly Dictionary<int,List<CollisionShape2D>> _rows=new();
    private readonly Dictionary<int,CollisionShape2D> _placedShapes=new();
    public WorldQueries Queries {get;}
    public IslandDefinition Definition => _island;
    public IslandDeltaState State => _state;
    public float LandLeftX => (float)WorldGrid.LandLeft;
    public float LandRightX => LandLeftX+_island.WidthCells*CellSize;
    public float WorldWidth => LandRightX+WaterMarginCells*CellSize;
    public float WorldTop => -IslandGenerationSettings.HighestLogicalLevel*CellSize;
    public float WorldBottom => -IslandGenerationSettings.DeepestLogicalLevel*CellSize;
    public Color SkyColor => _palette.Sky;
    public int RemainingNaturalFeatureCount => _island.NaturalFeatures.Count(Queries.NaturalPresent);
    public IslandView(IslandDefinition definition, IslandDeltaState state, IReadOnlyList<ResidentState>? residents=null)
    {
        _island=definition; _state=state; _residents=residents ?? Array.Empty<ResidentState>();
        _palette=BiomePalette.For(definition.Biome); Queries=new(definition,state);
        ProcessMode=ProcessModeEnum.Pausable; ZIndex=-10;
    }
    public override void _Ready() { PreparePhysics(); TextureFilter=TextureFilterEnum.Nearest; QueueRedraw(); }
    public static WorldPoint Point(Vector2 value) => new(value.X,value.Y);
    public static Vector2 Vector(WorldPoint value) => new((float)value.X,(float)value.Y);
    public static Rect2 Rect(WorldRect value) => new((float)value.X,(float)value.Y,(float)value.Width,(float)value.Height);
    public float LevelToWorldY(int level) => -level*CellSize;
    public Vector2 GetSuggestedSpawnPosition() => Vector(Queries.SafeSpawn());
    public Vector2 GetBoatArrivalSpawn(BoatSide side) => Vector(Queries.SafeSpawn(side));
    public Vector2 GetFeatureWorldPosition(NaturalFeatureSpawn f) => Vector(WorldGrid.Feet(f.CellX,f.SurfaceLevel));
    public Rect2 GetBuildCellRect(int x,int level) => Rect(WorldGrid.CellRect(new(x,level)));
    public Vector2 GetBuildCellCenter(int x,int level) => Vector(WorldGrid.Center(x,level));
    private Rect2 GetTerrainCellRect(int x,int level) => GetBuildCellRect(x,level);
    private bool IsSolidTerrainCell(UndergroundCell cell) => Queries.TerrainSolid(cell);
    private bool IsMineCellExposed(UndergroundCell cell) => Queries.Exposed(cell);
    public bool IsInMineShaft(Vector2 center) => Queries.IsLadder(Point(center));
    private Vector2 GetBoatWaterCenter(BoatSide side) => Vector(WorldGrid.BoatCenter(_island.WidthCells,side));
    public Vector2 GetBoatSiteCenter(BoatSide side)
    { int x=side==BoatSide.Left?0:_island.WidthCells-1; return Vector(WorldGrid.Feet(x,_island.SurfaceLevels[x])); }
    public bool TryWorldToBuildCell(Vector2 mouse,out int x,out int level)
    { var cell=WorldGrid.CellAt(Point(mouse)); x=cell.CellX; level=cell.LogicalLevel; return Queries.InBounds(cell); }
    public bool IsBoatBuilt(BoatSide side) => _state.IsBoatBuilt(side);
    public PlacedObjectState? FindPlacedObjectAt(int x,int level) => _state.PlacedObjects
        .Where(p=>p.CellX==x && p.LogicalLevel==level).OrderBy(p=>p.Layer==BuildLayer.Background?1:0).FirstOrDefault();
    public void ClearIndicators()
    {
        _highlightedFeatureId=null; _highlightedPlacementId=null; _highlightedMineCell=null; _entityHighlight=null; _residentHighlight=null;
        _highlightedFeatureProgress=0; _highlightedPlacementProgress=0; _highlightedMineProgress=0; QueueRedraw();
    }
    public void SetHarvestIndicator(int? id,float progress) { _highlightedFeatureId=id; _highlightedFeatureProgress=progress; QueueRedraw(); }
    public void SetPlacedHarvestIndicator(int? id,float progress) { _highlightedPlacementId=id; _highlightedPlacementProgress=progress; QueueRedraw(); }
    public void SetMineIndicator(UndergroundCell? cell,float progress) { _highlightedMineCell=cell; _highlightedMineProgress=progress; QueueRedraw(); }
    public void SetEntityIndicator(WorldEntityRef? entity)
    {
        _entityHighlight=null;
        if(entity.HasValue && Queries.TryEntity(entity.Value,out var placed,out var chest,out _))
            _entityHighlight=placed is not null?GetBuildCellRect(placed.CellX,placed.LogicalLevel):GetBuildCellRect(chest!.CellX,chest.LogicalLevel);
        QueueRedraw();
    }
    public void SetResidentIndicator(int? residentId) { _residentHighlight=residentId; QueueRedraw(); }
    public void SetBuildPreview(ItemType? item,int x,int level,BuildLayer layer,bool valid)
    { _previewItem=item; _previewCellX=x; _previewLogicalLevel=level; _previewLayer=layer; _previewValid=valid; QueueRedraw(); }
    public void ClearBuildPreview() { _previewItem=null; QueueRedraw(); }

    /// <summary>May run off-tree to stage a destination before touching the origin island.</summary>
    public void PreparePhysics()
    {
        if(_terrainCollisionBody is not null) return;
        _terrainCollisionBody=new StaticBody2D {Name="TerrainCollision",CollisionLayer=1,CollisionMask=1}; AddChild(_terrainCollisionBody);
        _placedCollisionBody=new StaticBody2D {Name="PlayerBuiltCollision",CollisionLayer=1,CollisionMask=1}; AddChild(_placedCollisionBody);
        _boatCollisionBody=new StaticBody2D {Name="BoatCollision",CollisionLayer=1,CollisionMask=1}; AddChild(_boatCollisionBody);
        _structureCollisionBody=new StaticBody2D {Name="GeneratedStructureCollision",CollisionLayer=1,CollisionMask=1}; AddChild(_structureCollisionBody);
        for(int level=IslandGenerationSettings.DeepestLogicalLevel;level<=_island.SurfaceLevels.Max();level++) RefreshTerrainRow(level);
        // This foundation is outside the 201 editable rows; mining the lowest row cannot create an endless fall.
        AddShape(_terrainCollisionBody,new WorldRect(LandLeftX,WorldBottom+CellSize,_island.WidthCells*CellSize,CellSize));
        RefreshPlacedCollision(); RefreshBoatCollision(); RefreshStructureCollision();
    }
    public void RefreshTerrainRow(int level)
    {
        if(_terrainCollisionBody is null) return;
        if(_rows.Remove(level,out var old)) foreach(var shape in old) { _terrainCollisionBody.RemoveChild(shape); shape.QueueFree(); }
        var shapes=new List<CollisionShape2D>(); int start=-1;
        for(int x=0;x<=_island.WidthCells;x++)
        {
            bool solid=x<_island.WidthCells && Queries.TerrainSolid(new(x,level));
            if(solid && start<0) start=x;
            if(!solid && start>=0)
            {
                shapes.Add(AddShape(_terrainCollisionBody,new WorldRect(LandLeftX+start*CellSize,LevelToWorldY(level),(x-start)*CellSize,CellSize)));
                start=-1;
            }
        }
        _rows[level]=shapes; QueueRedraw();
    }
    public void RefreshPlacedCollision()
    {
        if(_placedCollisionBody is null) return;
        var solid=_state.PlacedObjects.Where(p=>PlacementRules.IsSolid(p.Item,p.Layer)).ToDictionary(p=>p.PlacementId);
        foreach(var id in _placedShapes.Keys.Where(id=>!solid.ContainsKey(id)).ToArray())
        { var shape=_placedShapes[id]; _placedCollisionBody.RemoveChild(shape); shape.QueueFree(); _placedShapes.Remove(id); }
        foreach(var (id,p) in solid)
            if(!_placedShapes.ContainsKey(id)) _placedShapes[id]=AddShape(_placedCollisionBody,WorldGrid.CellRect(new(p.CellX,p.LogicalLevel)));
        QueueRedraw();
    }
    public void RefreshStructureCollision()
    {
        if(_structureCollisionBody is null) return;
        foreach(Node node in _structureCollisionBody.GetChildren())
        { _structureCollisionBody.RemoveChild(node); node.QueueFree(); }

        foreach(var layout in Queries.StructureLayouts)
        {
            foreach(var group in layout.SolidCells.GroupBy(c=>c.LogicalLevel))
            {
                int start=-1,previous=-2;
                foreach(int x in group.Select(c=>c.CellX).OrderBy(x=>x).Append(int.MaxValue))
                {
                    if(start<0) { if(x!=int.MaxValue) { start=x; previous=x; } continue; }
                    if(x==previous+1) { previous=x; continue; }
                    AddShape(_structureCollisionBody,new WorldRect(
                        LandLeftX+start*CellSize,LevelToWorldY(group.Key),
                        (previous-start+1)*CellSize,CellSize));
                    if(x==int.MaxValue) break;
                    start=x; previous=x;
                }
            }
        }
        QueueRedraw();
    }

    public void RefreshBoatCollision(BoatSide? pendingArrival=null)
    {
        if(_boatCollisionBody is null) return;
        foreach(Node node in _boatCollisionBody.GetChildren()) { _boatCollisionBody.RemoveChild(node); node.QueueFree(); }
        foreach(var side in Enum.GetValues<BoatSide>()) if(_state.IsBoatBuilt(side) || pendingArrival==side) AddShape(_boatCollisionBody,WorldGrid.BoatHull(_island.WidthCells,side));
        QueueRedraw();
    }
    private static CollisionShape2D AddShape(Node parent, WorldRect r)
    {
        var shape=new CollisionShape2D {Shape=new RectangleShape2D {Size=new((float)r.Width,(float)r.Height)},Position=Vector(r.Center)};
        parent.AddChild(shape); return shape;
    }

    #region Procedural pixel presentation (read-only)

    private double _artClock;
    private int _artFrame;
    private Transform2D _lastArtTransform;
    private Rect2 _artView;

    public override void _Process(double delta)
    {
        _artClock += delta;
        int frame = (int)(_artClock * 8.0) % 4;
        Transform2D transform = GetCanvasTransform();
        if (frame != _artFrame || transform != _lastArtTransform)
        {
            _artFrame = frame;
            _lastArtTransform = transform;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        Transform2D inverse = GetCanvasTransform().AffineInverse();
        Vector2 a = inverse * Vector2.Zero;
        Vector2 b = inverse * GetViewportRect().Size;
        _artView = new Rect2(new Vector2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)),
            new Vector2(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y))).Grow(180.0f);

        DrawPixelOcean();
        DrawPixelUnderground();
        DrawPixelTerrain();
        DrawPixelLadder();
        DrawPixelStructures();
        DrawPixelPlaced(true);
        DrawPixelNature();
        DrawPixelChests();
        DrawPixelResidents();
        DrawPixelPlaced(false);
        DrawPixelBoats();
        DrawPixelIndicators();
    }

    private (int Left, int Right, int Bottom, int Top) ArtCellBounds()
    {
        int left = Math.Clamp(Mathf.FloorToInt((_artView.Position.X - LandLeftX) / CellSize), 0, _island.WidthCells - 1);
        int right = Math.Clamp(Mathf.CeilToInt((_artView.End.X - LandLeftX) / CellSize), 0, _island.WidthCells - 1);
        int bottom = Math.Max(IslandGenerationSettings.DeepestLogicalLevel, Mathf.FloorToInt(-_artView.End.Y / CellSize) - 1);
        int top = Math.Min(IslandGenerationSettings.HighestLogicalLevel, Mathf.CeilToInt(-_artView.Position.Y / CellSize) + 1);
        return (left, right, bottom, top);
    }

    private void DrawPixelUnderground()
    {
        var bounds = ArtCellBounds();
        for (int x = bounds.Left; x <= bounds.Right; x++)
            for (int level = bounds.Bottom; level <= Math.Min(0, bounds.Top); level++)
            {
                // The dark layer begins at exactly level 0. Above it a hole reveals sky.
                if (IsSolidTerrainCell(new UndergroundCell(x, level))) continue;
                PixelAtlas.DrawRect(this, $"cave/{PixelAtlas.Variant(x, level)}", GetTerrainCellRect(x, level));
            }
    }

    private void DrawPixelTerrain()
    {
        var bounds = ArtCellBounds();
        for (int x = bounds.Left; x <= bounds.Right; x++)
            for (int level = bounds.Bottom; level <= Math.Min(bounds.Top, _island.SurfaceLevels[x]); level++)
            {
                var cell = new UndergroundCell(x, level);
                if (!IsSolidTerrainCell(cell)) continue;
                Rect2 rect = GetTerrainCellRect(x, level);
                int variant = PixelAtlas.Variant(x, level);
                bool geologicalRock = level < Math.Min(0, _island.SurfaceLevels[x] - 3);
                PixelAtlas.DrawRect(this, $"terrain/{_island.Biome}/{(geologicalRock ? "rock" : "soil")}/{variant}", rect);

                if (!IsSolidTerrainCell(new UndergroundCell(x, level + 1)))
                {
                    string capBiome = geologicalRock ? "Mountain" : _island.Biome.ToString();
                    PixelAtlas.DrawRect(this, $"cap/{capBiome}/{variant}",
                        new Rect2(rect.Position, new Vector2(CellSize, 12.0f)));
                }
                if (!IsSolidTerrainCell(new UndergroundCell(x - 1, level)))
                    DrawRect(new Rect2(rect.Position, new Vector2(2, CellSize)), PixelAtlas.ToColor(PxColor.Bark0));
                if (!IsSolidTerrainCell(new UndergroundCell(x + 1, level)))
                    DrawRect(new Rect2(rect.Position + new Vector2(CellSize - 2, 0), new Vector2(2, CellSize)), PixelAtlas.ToColor(PxColor.Stone0));
                if (_island.UndergroundOres.TryGetValue(cell, out UndergroundOreSpawn ore) && IsMineCellExposed(cell))
                {
                    Color copper = PixelAtlas.ToColor(PxColor.Bark3);
                    DrawRect(new Rect2(rect.Position + new Vector2(10, 14), new Vector2(8, 6)), copper);
                    DrawRect(new Rect2(rect.Position + new Vector2(12, 14), new Vector2(4, 2)), PixelAtlas.ToColor(PxColor.Sand3));
                    DrawRect(new Rect2(rect.Position + new Vector2(30, 28), new Vector2(6, 8)), copper);
                    if (ore.Richness > 1)
                        DrawRect(new Rect2(rect.Position + new Vector2(26, 8), new Vector2(8, 4)), copper);
                }
            }
    }

    private void DrawPixelOcean()
    {
        DrawWaterRange(0, LandLeftX);
        DrawWaterRange(LandRightX, WorldWidth);
    }

    private void DrawWaterRange(float left, float right)
    {
        int start = Math.Max(0, Mathf.FloorToInt((_artView.Position.X - left) / CellSize));
        int end = Math.Min(Mathf.CeilToInt((right - left) / CellSize), Mathf.CeilToInt((_artView.End.X - left) / CellSize));
        for (int x = start; x < end; x++)
        {
            float worldX = left + x * CellSize;
            for (int y = 0; y < 6; y++)
            {
                Rect2 rect = new(worldX, y * CellSize, CellSize, CellSize);
                if (_artView.Intersects(rect))
                    PixelAtlas.DrawRect(this, $"water/{(_artFrame + x + y) % 4}", rect);
            }
            if (_artView.Intersects(new Rect2(worldX, 0, CellSize, 12)))
                PixelAtlas.DrawRect(this, $"waterline/{(_artFrame + x) % 4}", new Rect2(worldX, 0, CellSize, 12));
        }
    }

    private void DrawPixelLadder()
    {
        int highestSurface = Enumerable.Range(_island.MineShaftLeftCell, _island.MineShaftWidthCells)
            .Select(x => _island.SurfaceLevels[x]).Max();
        float center = LandLeftX + (_island.MineShaftLeftCell + _island.MineShaftWidthCells / 2.0f) * CellSize;
        float top = LevelToWorldY(highestSurface) - 5.0f;
        float bottom = WorldBottom + CellSize;
        int first = Math.Max(0, Mathf.FloorToInt((_artView.Position.Y - top) / CellSize));
        int last = Math.Min(Mathf.CeilToInt((bottom - top) / CellSize), Mathf.CeilToInt((_artView.End.Y - top) / CellSize));
        if (center < _artView.Position.X || center > _artView.End.X) return;
        for (int segment = first; segment < last; segment++)
        {
            float y = top + segment * CellSize;
            PixelAtlas.DrawRect(this, "ladder", new Rect2(center - 16, y, 32, Math.Min(CellSize, bottom - y)));
        }
    }

    private void DrawPixelStructures()
    {
        foreach (GeneratedStructureDefinition structure in _island.GeneratedStructures)
        {
            GeneratedStructureLayout layout=Queries.StructureLayouts.First(l=>l.StructureId==structure.StructureId);
            string material=structure.Kind==GeneratedStructureKind.Castle?"Stone":"Wood";
            Color backgroundTint=structure.Kind==GeneratedStructureKind.Castle
                ? new Color(0.58f,0.62f,0.68f,0.62f)
                : new Color(0.62f,0.48f,0.34f,0.62f);

            // Faint silhouette preserves the biome-specific architectural identity,
            // while the grid pieces below form the actual playable cutaway.
            float centerX=LandLeftX+(structure.CenterCellX+0.5f)*CellSize;
            float groundY=LevelToWorldY(structure.BaseSurfaceLevel);
            string silhouette=structure.Kind==GeneratedStructureKind.Castle?"castle":"house";
            if(new Rect2(centerX-250,groundY-320,500,330).Intersects(_artView))
                PixelAtlas.DrawBottom(this,$"{silhouette}/{_island.Biome}",new Vector2(centerX,groundY+2),
                    modulate:new Color(1,1,1,0.10f));

            foreach(var cell in layout.BackgroundCells)
            {
                Rect2 rect=GetBuildCellRect(cell.CellX,cell.LogicalLevel);
                if(!_artView.Intersects(rect)) continue;
                int variant=PixelAtlas.Variant(cell.CellX,cell.LogicalLevel);
                PixelAtlas.DrawRect(this,$"block/{material}/{variant}",rect,modulate:backgroundTint);
            }

            foreach(var cell in layout.SolidCells)
            {
                Rect2 rect=GetBuildCellRect(cell.CellX,cell.LogicalLevel);
                if(!_artView.Intersects(rect)) continue;
                int variant=PixelAtlas.Variant(cell.CellX,cell.LogicalLevel);
                PixelAtlas.DrawRect(this,$"block/{material}/{variant}",rect);
            }

            foreach(var cell in layout.LadderCells)
            {
                Rect2 rect=GetBuildCellRect(cell.CellX,cell.LogicalLevel);
                if(_artView.Intersects(rect))
                    PixelAtlas.DrawRect(this,"ladder",new Rect2(rect.GetCenter().X-16,rect.Position.Y,32,CellSize));
            }

            // Doorway edge highlights make the entrance readable against the background wall.
            Rect2 door=GetBuildCellRect(layout.DoorCellX,layout.BaseLogicalLevel+1);
            if(_artView.Intersects(door))
            {
                Color edge=PixelAtlas.ToColor(PxColor.Gold2);
                DrawRect(new Rect2(door.Position,new Vector2(3,door.Size.Y*2)),edge);
                DrawRect(new Rect2(new Vector2(door.End.X-3,door.Position.Y),new Vector2(3,door.Size.Y*2)),edge);
            }
        }
    }

    private void DrawPixelChests()
    {
        foreach (GeneratedStructureDefinition structure in _island.GeneratedStructures)
        {
            GeneratedChestDefinition chest = structure.Chest;
            Rect2 rect = GetBuildCellRect(chest.CellX, chest.LogicalLevel);
            if (!_artView.Intersects(rect)) continue;
            bool empty = _state.GeneratedChestContents.TryGetValue(chest.ChestId, out var contents)
                ? contents.IsEmpty : _state.IsGeneratedChestLooted(chest.ChestId);
            string state = empty ? "open" : "0";
            PixelAtlas.DrawBottom(this, $"prop/Chest/{state}", new Vector2(rect.GetCenter().X, rect.End.Y));
        }
    }

    private string ArtNaturalKey(NaturalFeatureSpawn feature)
    {
        int v = Math.Abs(feature.Variant % 3);
        int large = HarvestRules.IsLarge(feature) ? 1 : 0;
        return feature.Kind switch
        {
            NaturalFeatureKind.Tree => $"tree/{large}/{v}",
            NaturalFeatureKind.Pine => $"pine/{large}/{v}",
            NaturalFeatureKind.Palm => $"palm/{large}/{v}",
            NaturalFeatureKind.Cactus => $"cactus/{large}/{v}",
            NaturalFeatureKind.Rock => $"{(_island.Biome == BiomeType.Snowy ? "rock-snow" : "rock")}/{v}",
            NaturalFeatureKind.Flower => v switch { 0 => "flower/RedFlower", 1 => "flower/YellowFlower", _ => "flower/BlueFlower" },
            NaturalFeatureKind.Grass => $"{(_island.Biome == BiomeType.Jungle ? "grass-jungle" : "grass")}/{v}",
            NaturalFeatureKind.Bush => $"bush/{v}",
            NaturalFeatureKind.Mushroom => $"mushroom/{(feature.Variant == 0 ? 0 : 1)}",
            _ => "missing",
        };
    }

    private void DrawPixelNature()
    {
        foreach (NaturalFeatureSpawn feature in _island.NaturalFeatures)
        {
            if (!Queries.NaturalPresent(feature)) continue;
            Vector2 feet = GetFeatureWorldPosition(feature);
            if (!new Rect2(feet - new Vector2(80, 160), new Vector2(160, 165)).Intersects(_artView)) continue;
            string key = ArtNaturalKey(feature);
            PixelAtlas.DrawBottom(this, key, feet);
            if (_highlightedFeatureId == feature.FeatureId)
            {
                float height = PixelAtlas.Get(key).GetHeight() * PixelAtlas.WorldPixelSize;
                PixelAtlas.Bar(this, feet - new Vector2(24, height + 10), 48, _highlightedFeatureProgress);
            }
        }
    }

    private void DrawPixelResidents()
    {
        foreach (ResidentState resident in _residents)
        {
            if (resident.Status != ResidentStatus.World || resident.IslandId != _island.IslandId) continue;
            Vector2 feet = Vector(ResidentService.Feet(resident,_island));
            Rect2 bounds = Rect(ResidentService.Rect(resident,_island)).Grow(34);
            if (!bounds.Intersects(_artView)) continue;
            string key;
            if(resident.Kind==ResidentKind.Bear && resident.Worker is { Assigned:true } worker)
            {
                bool left=worker.TravelToCellX<worker.TravelFromCellX;
                int frame=(int)((Time.GetTicksMsec()/180UL)%4UL);
                key=worker.Phase switch
                {
                    WorkerPhase.Working => $"bear/work/{frame}"+(left?"/left":""),
                    WorkerPhase.TravellingToTarget or WorkerPhase.ReturningToChest => $"bear/walk/{(int)((Time.GetTicksMsec()/120UL)%8UL)}"+(left?"/left":""),
                    _ => $"bear/idle/{resident.AppearanceVariant % 4}",
                };
            }
            else key = resident.Kind switch
            {
                ResidentKind.Cat => "resident/cat",
                ResidentKind.PolarBear => "resident/polar",
                ResidentKind.Monkey => "resident/monkey",
                _ => $"bear/idle/{resident.AppearanceVariant % 4}",
            };
            PixelAtlas.DrawBottom(this,key,feet);
            PixelAtlas.Bar(this,feet-new Vector2(28,104),56,resident.Sympathy/100.0f);
            if(resident.Worker is { Assigned:true } w)
            {
                float progress=w.TravelTotalSeconds>1e-9 ? (float)Math.Clamp(1.0-w.RemainingSeconds/w.TravelTotalSeconds,0,1) :
                    w.Phase==WorkerPhase.Working ? (float)Math.Clamp(1.0-w.RemainingSeconds/1.5,0,1) : 0;
                PixelAtlas.Bar(this,feet-new Vector2(28,94),56,progress);
            }
            if (resident.Kind is ResidentKind.PolarBear or ResidentKind.Monkey)
            {
                string chest = resident.Kind==ResidentKind.PolarBear ? "IceChest" : "GoldenChest";
                string state = resident.SpecialChestClaimed ? "open" : "0";
                PixelAtlas.DrawBottom(this,$"prop/{chest}/{state}",feet+new Vector2(50,0));
            }
            if (_residentHighlight == resident.ResidentId)
                DrawPixelCorners(bounds,PixelAtlas.ToColor(PxColor.Gold3));
        }
    }

    private void DrawPixelPlaced(bool background)
    {
        foreach (PlacedObjectState placed in _state.PlacedObjects)
        {
            if ((placed.Layer == BuildLayer.Background) != background) continue;
            Rect2 rect = GetBuildCellRect(placed.CellX, placed.LogicalLevel);
            if (!rect.Grow(160).Intersects(_artView)) continue;
            DrawPixelObject(placed.Item, rect, placed.Layer, placed, false);
        }
    }

    private void DrawPixelObject(ItemType item, Rect2 rect, BuildLayer layer, PlacedObjectState? state, bool preview)
    {
        Color tint = Colors.White;
        if (layer == BuildLayer.Background) tint = new Color(0.64f, 0.68f, 0.66f, 1);
        if (preview) tint.A = 0.48f;
        Vector2 feet = new(rect.GetCenter().X, rect.End.Y);
        string key;
        if (PlacementRules.IsBlock(item))
        {
            int variant = PixelAtlas.Variant(Mathf.RoundToInt(rect.Position.X / CellSize), Mathf.RoundToInt(rect.Position.Y / CellSize));
            PixelAtlas.DrawRect(this, $"block/{item}/{variant}", rect, modulate: tint);
            return;
        }
        if (item == ItemType.Sapling)
        {
            double age = state?.GrowthSeconds ?? 0.0;
            if (age >= WorldSimulationService.SaplingGrowthSeconds && state is not null)
            {
                int size = PlacedHarvestRules.IsLargeMatureTree(state) ? 1 : 0;
                string species = state.PlantKind switch { NaturalFeatureKind.Pine => "pine", NaturalFeatureKind.Palm => "palm",
                    NaturalFeatureKind.Cactus => "cactus", _ => "tree" };
                key = $"{species}/{size}/{PixelAtlas.Variant(state.CellX, state.PlacementId, 3)}";
            }
            else
            {
                int stage = Math.Clamp((int)(age / WorldSimulationService.SaplingGrowthSeconds * 3), 0, 2);
                key = $"sapling/{stage}";
            }
        }
        else if (PlacementRules.IsFlower(item)) key = $"flower/{item}";
        else if (item == ItemType.Grass) key = "grass/0";
        else
        {
            int frame = item is ItemType.Forge or ItemType.Cauldron ? _artFrame : 0;
            key = $"prop/{item}/{frame}";
        }
        PixelAtlas.DrawBottom(this, key, feet, modulate: tint);
        if (!preview && state is not null)
        {
            if (state.Item == ItemType.Beehive)
            {
                for (int i = 0; i < Math.Min(state.StoredOutput, WorldSimulationService.HoneyCapacity); i++)
                    PixelAtlas.DrawBottom(this, "icon/Honey", feet + new Vector2(-16 + i * 16, -48), 0.6f);
                // Bees are visual only. Their paths never determine honey production.
                if (state.ProductionSeconds > 0 || state.StoredOutput > 0)
                    for (int i = 0; i < 2; i++)
                    {
                        double angle = _artClock * 1.3 + i * 2.7 + state.PlacementId;
                        Vector2 offset = new((float)Math.Cos(angle) * 22, -46 + (float)Math.Sin(angle * 1.3) * 9);
                        PixelAtlas.DrawBottom(this, $"bee/{_artFrame % 2}", feet + offset, 1.0f);
                    }
            }
            if (_highlightedPlacementId == state.PlacementId)
            {
                float height = PixelAtlas.Get(key).GetHeight() * PixelAtlas.WorldPixelSize;
                PixelAtlas.Bar(this, feet - new Vector2(24, height + 10), 48, _highlightedPlacementProgress);
            }
        }
    }

    private void DrawPixelBoats()
    {
        foreach (BoatSide side in Enum.GetValues<BoatSide>())
        {
            int start = side == BoatSide.Left ? 0 : _island.WidthCells - BoatConstructionShoreCells;
            float shoreX = LandLeftX + start * CellSize + BoatConstructionShoreCells * CellSize / 2.0f;
            int edge = side == BoatSide.Left ? 0 : _island.WidthCells - 1;
            float groundY = LevelToWorldY(_island.SurfaceLevels[edge]);
            if (new Rect2(shoreX - 120, groundY - 90, 240, 120).Intersects(_artView))
                PixelAtlas.DrawBottom(this, "shore-site", new Vector2(shoreX, groundY + 2));
            if (!_state.IsBoatBuilt(side)) continue;
            Vector2 water = GetBoatWaterCenter(side); // Identical origin to the hull collider.
            Rect2 boatRect = new(water.X - 48, water.Y + 7 - 66, 96, 84);
            if (_artView.Intersects(boatRect))
                PixelAtlas.DrawRect(this, "boat", boatRect, flip: side == BoatSide.Left);
        }
    }

    private void DrawPixelIndicators()
    {
        if (_highlightedMineCell is not null)
        {
            Rect2 rect = GetTerrainCellRect(_highlightedMineCell.Value.CellX, _highlightedMineCell.Value.LogicalLevel);
            DrawPixelCorners(rect, PixelAtlas.ToColor(PxColor.Gold3));
            PixelAtlas.Bar(this, rect.Position + new Vector2(4, 4), CellSize - 8, _highlightedMineProgress);
            if (_highlightedMineProgress > 0.3f)
            {
                // Cosmetic chips/cracks on the hovered tile, not a second mining model.
                Vector2 c = rect.GetCenter();
                DrawLine(c - new Vector2(5, 8), c + new Vector2(1, 0), PixelAtlas.ToColor(PxColor.Ink), 2);
                if (_highlightedMineProgress > 0.6f)
                {
                    DrawLine(c, c + new Vector2(9, 6), PixelAtlas.ToColor(PxColor.Ink), 2);
                    DrawLine(c, c + new Vector2(-4, 10), PixelAtlas.ToColor(PxColor.Ink), 2);
                }
            }
        }
        if (_entityHighlight is not null) DrawPixelCorners(_entityHighlight.Value, PixelAtlas.ToColor(PxColor.Gold3));
        if (_previewItem is not null)
        {
            Rect2 rect = GetBuildCellRect(_previewCellX, _previewLogicalLevel);
            Color ink = PixelAtlas.ToColor(_previewValid ? PxColor.Green4 : PxColor.Red2);
            DrawRect(rect, new Color(ink.R, ink.G, ink.B, 0.16f));
            DrawPixelObject(_previewItem.Value, rect, _previewLayer, null, true);
            DrawPixelCorners(rect, ink);
        }
    }

    private void DrawPixelCorners(Rect2 rect, Color ink)
    {
        const float thickness = 2, length = 10;
        foreach (Vector2 corner in new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y),
            new Vector2(rect.Position.X, rect.End.Y), rect.End })
        {
            float sx = corner.X == rect.Position.X ? 1 : -1;
            float sy = corner.Y == rect.Position.Y ? 1 : -1;
            DrawLine(corner, corner + new Vector2(length * sx, 0), ink, thickness);
            DrawLine(corner, corner + new Vector2(0, length * sy), ink, thickness);
        }
    }

    #endregion
}
