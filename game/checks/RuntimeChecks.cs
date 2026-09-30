using BearAdventure.Domain.Gameplay;
using BearAdventure.Domain.World;
using BearAdventure.Interaction;
using BearAdventure.Player;
using BearAdventure.Rendering.PixelArt;
using BearAdventure.UI;
using BearAdventure.World;
using Godot;

namespace BearAdventure.Checks;

/// <summary>Isolated engine smoke scene: no Main, no user save reads/writes.</summary>
public partial class RuntimeChecks : Node
{
    private int _passed;
    private void Assert(bool condition,string name)
    { if(!condition) throw new InvalidOperationException(name); _passed++; GD.Print("ENGINE PASS "+name); }
    private async Task PhysicsFrames(int count)
    { for(int n=0;n<count;n++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
    private async Task Frames(int count)
    { for(int n=0;n<count;n++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    public override async void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;
        try
        {
            var open=new HashSet<UndergroundCell>();
            for(int x=38;x<40;x++) for(int l=0;l>=-100;l--) open.Add(new(x,l));
            var d=new IslandDefinition {IslandId=0,IslandSeed=100,GeneratorVersion=1,Biome=BiomeType.Forest,WidthCells=80,
                BoatSiteWidthCells=10,SurfaceLevels=new int[80],NaturalFeatures=Array.Empty<NaturalFeatureSpawn>(),
                UndergroundOpenCells=open,UndergroundOres=new Dictionary<UndergroundCell,UndergroundOreSpawn>(),
                MineShaftLeftCell=38,MineShaftWidthCells=2,
                GeneratedStructures=new[]{new GeneratedStructureDefinition(0,GeneratedStructureKind.House,55,6,0,
                    new GeneratedChestDefinition(1000,55,1,new Dictionary<ItemType,int>{{ItemType.Wood,4}}))}};
            var state=new IslandDeltaState(); state.SetBoatBuilt(BoatSide.Left,true); state.SetBoatBuilt(BoatSide.Right,true);
            var resident=new ResidentState(1,"Rowan",ResidentKind.Bear,0,0,24,1,0){Coins=10,MaxCoins=10,Sympathy=100};
            var residents=new List<ResidentState>{resident};
            var world=new IslandView(d,state,residents); world.PreparePhysics(); AddChild(world);
            await PhysicsFrames(2);
            var structureLayout=world.Queries.StructureLayouts.Single();
            var structureBody=world.GetNode<StaticBody2D>("GeneratedStructureCollision");
            Assert(structureBody.GetChildCount()>0,"generated structure creates collision geometry");
            Assert(!world.Queries.Solid(new(structureLayout.DoorCellX,1)),"generated house doorway stays open");
            var input=new GameplayInput(); AddChild(input); input.State.ObserveNeutral(true);
            var player=new BearController {ProcessMode=ProcessModeEnum.Pausable,InputState=input.State}; AddChild(player);
            player.ConfigureIsland(world.LandLeftX,world.LandRightX,world.GetSuggestedSpawnPosition());
            player.ConfigureCamera(0,world.WorldWidth,world.WorldTop,world.WorldBottom);
            _=PixelAtlas.Get("bear/idle/0");
            Assert(PixelAtlas.CachedTextureCount>=279,"runtime atlas contains reference-art catalog");
            foreach(var side in Enum.GetValues<BoatSide>())
            {
                var center=WorldGrid.BoatCenter(d.WidthCells,side);
                player.PlaceAt(new((float)center.X,-180)); player.ClimbEnabled=false;
                await PhysicsFrames(100);
                Assert(player.IsOnFloor(),$"bear lands on {side} boat");
                Assert(Math.Abs(player.Position.Y+29-WorldGrid.BoatHull(d.WidthCells,side).Y)<3,$"{side} hull visual/physics alignment");
            }
            player.PlaceAt(IslandView.Vector(WorldGrid.Center(38,-10)));
            player.ClimbEnabled=true; player.MovementLocked=true;
            float before=player.Position.Y; await PhysicsFrames(20);
            Assert(Math.Abs(player.Position.Y-before)<0.1,"mining lock holds bear on ladder");
            player.MovementLocked=false; player.ClimbEnabled=false; player.PlaceAt(world.GetSuggestedSpawnPosition());
            var hud=new GameHud(); AddChild(hud); hud.ConfigureInventory(new InventoryState()); hud.ConfigureResidentProvider(()=>residents);
            hud.ModalChanged+=input.SetModal; input.Cancelled+=player.CancelMotion; input.MenuRequested+=hud.HandleMenu;
            foreach(var menu in new[]{MenuCommand.Inventory,MenuCommand.Crafting,MenuCommand.Map,MenuCommand.Friends})
            {
                hud.HandleMenu(menu); Assert(GetTree().Paused && input.BlocksWorld,"modal pauses "+menu);
                var old=player.Position; input.State.Press(GameAction.Right); input.State.Press(GameAction.Build); input.State.Press(GameAction.Harvest);
                await Frames(5); Assert(player.Position==old && !input.State.Held(GameAction.Build),"world input blocked by "+menu);
                input._Input(new InputEventKey {Keycode=Key.Escape,PhysicalKeycode=Key.Escape,Pressed=true});
                Assert(!hud.AnyPanelOpen && !GetTree().Paused,"Escape closes "+menu);
            }
            hud.OpenResident(resident);
            Assert(hud.TradeOpen && GetTree().Paused,"resident trade pauses world");
            input._Input(new InputEventKey {Keycode=Key.Escape,PhysicalKeycode=Key.Escape,Pressed=true});
            Assert(!hud.AnyPanelOpen && !GetTree().Paused,"Escape closes resident trade");
            var workerChest=state.AddPlacedObject(ItemType.Chest,26,1,BuildLayer.Solid);
            resident.Worker=new WorkerState {HomeChestPlacementId=workerChest.PlacementId,WorkCenterCellX=26,WorkCenterLogicalLevel=1,CurrentCellX=24,CurrentLogicalLevel=1};
            hud.OpenWorker(resident,new[]{workerChest});
            Assert(hud.WorkerOpen && GetTree().Paused,"worker settings pause world");
            input._Input(new InputEventKey {Keycode=Key.Escape,PhysicalKeycode=Key.Escape,Pressed=true});
            Assert(!hud.AnyPanelOpen && !GetTree().Paused,"Escape closes worker settings");
            foreach(var station in new[]{ItemType.Forge,ItemType.Anvil,ItemType.Cauldron})
            {
                hud.OpenStation(new(0,WorldEntityKind.Placed,1),station);
                input._Input(new InputEventKey {Keycode=Key.Escape,PhysicalKeycode=Key.Escape,Pressed=true});
                Assert(!hud.AnyPanelOpen && !GetTree().Paused,"Escape closes "+station);
            }
            hud.OpenStorage(new(0,WorldEntityKind.Placed,1),new InventoryState(),false);
            input._Input(new InputEventKey {Keycode=Key.Escape,PhysicalKeycode=Key.Escape,Pressed=true});
            Assert(!hud.AnyPanelOpen && !GetTree().Paused,"Escape closes storage");
            input.State.ObserveNeutral(true); input.State.Press(GameAction.Build);
            input._Input(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=false});
            Assert(!input.State.Held(GameAction.Build),"release observed before GUI propagation");
            input.State.Press(GameAction.Harvest); input.CancelActions();
            Assert(!input.State.Held(GameAction.Harvest) && input.State.AwaitingRelease,"held action suppressed after cancellation");
            input.State.ObserveNeutral(true);
            await PhysicsFrames(2);
            var terrain=world.GetNode<StaticBody2D>("TerrainCollision"); Node originalRow=terrain.GetChild(0);
            state.MarkUndergroundCellMined(new(20,0)); world.RefreshTerrainRow(0);
            await PhysicsFrames(2);
            Assert(GodotObject.IsInstanceValid(originalRow) && originalRow.GetParent()==terrain,"mining preserves untouched collision rows");
            var query=new PhysicsPointQueryParameters2D {Position=IslandView.Vector(WorldGrid.Center(20,0)),CollisionMask=1};
            Assert(world.GetWorld2D().DirectSpaceState.IntersectPoint(query).Count==0,"mined tile loses collision");
            query.Position=IslandView.Vector(WorldGrid.Center(21,0));
            Assert(world.GetWorld2D().DirectSpaceState.IntersectPoint(query).Count>0,"neighbor tile retains collision");
            var expected=WorldGrid.Center(22,0); var screen=world.GetCanvasTransform()*IslandView.Vector(expected);
            var restored=world.GetCanvasTransform().AffineInverse()*screen;
            Assert(WorldGrid.CellAt(IslandView.Point(restored))==new UndergroundCell(22,0),"hover conversion follows camera transform");
            GD.Print($"ENGINE CHECKS PASSED: {_passed}"); GetTree().Quit(0);
        }
        catch(Exception e) { GD.PushError("ENGINE CHECKS FAILED: "+e); GetTree().Paused=false; GetTree().Quit(1); }
    }
}
