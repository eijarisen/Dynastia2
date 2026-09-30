using BearAdventure.Domain.Gameplay;
using BearAdventure.Domain.Simulation;
using BearAdventure.Domain.World;
using BearAdventure.Interaction;
using BearAdventure.Persistence;
using BearAdventure.Player;
using BearAdventure.Rendering;
using BearAdventure.UI;
using BearAdventure.World;
using Godot;
using IOFile = System.IO.File;

namespace BearAdventure;

public partial class Main : Node2D
{
    private const string DefaultSeed="bear-adventure-development-001";
    private readonly IslandGenerator _generator=new();
    private readonly SaveGameStore _saves=new();
    private readonly WorldSimulationService _simulation=new();
    private GameSessionState? _session;
    private IslandView? _island;
    private BearController _bear=null!;
    private HarvestController _harvest=null!;
    private BuildingController _building=null!;
    private GameHud _hud=null!;
    private GameplayInput _input=null!;
    private ParallaxBackdrop _backdrop=null!;
    private bool _saveAllowed, _dirty, _quitting;
    private double _autosave, _saveDelay=-1;
    private string _savePath="";
    private string _originalPath="";
    private string _selectorPath="";

    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;
        ProcessPhysicsPriority=-100;
        GetTree().AutoAcceptQuit=false;
        _originalPath=ProjectSettings.GlobalizePath("user://bear-adventure-save.json");
        _selectorPath=ProjectSettings.GlobalizePath("user://bear-adventure-selected-save.txt");
        _savePath=SelectedSavePath();
        var layer=new CanvasLayer {Name="ParallaxLayer",Layer=-100,ProcessMode=ProcessModeEnum.Pausable}; AddChild(layer);
        _backdrop=new ParallaxBackdrop {Name="ParallaxBackdrop",ProcessMode=ProcessModeEnum.Pausable}; layer.AddChild(_backdrop);
        _hud=new GameHud {Name="Hud"}; AddChild(_hud);
        _bear=new BearController {Name="Bear",ProcessMode=ProcessModeEnum.Pausable}; AddChild(_bear);
        _harvest=new HarvestController {Name="HarvestController",ProcessMode=ProcessModeEnum.Pausable,ProcessPhysicsPriority=-50}; AddChild(_harvest);
        _building=new BuildingController {Name="BuildingController",ProcessMode=ProcessModeEnum.Pausable,ProcessPhysicsPriority=-40}; AddChild(_building);
        // Added last: it observes releases and menu keys before GUI/gameplay input dispatch.
        _input=new GameplayInput {Name="GameplayInput"}; AddChild(_input);
        _bear.InputState=_input.State;
        _harvest.BuildingSelected=()=>_building.SelectedItem.HasValue;
        _input.MenuRequested+=_hud.HandleMenu;
        _input.Cancelled+=CancelTransientActions;
        _input.FocusLost+=()=>{if(_session is not null && !_hud.AnyPanelOpen) _hud.ShowPause("Window lost focus.");};
        _hud.ModalChanged+=_input.SetModal;
        _hud.PlaceableItemSelected+=_building.SelectItem;
        _hud.ConfigureResidentProvider(()=>_session?.Residents ?? Array.Empty<ResidentState>());
        _hud.ConfigureMapProvider(BuildMapEntries);
        _hud.CraftRequested+=Craft;
        _hud.StationCraftRequested+=CraftAtStation;
        _hud.TransferRequested+=Transfer;
        _hud.TradeRequested+=TradeWithResident;
        _hud.InviteResidentRequested+=InviteResident;
        _hud.SpecialRewardRequested+=ClaimSpecialReward;
        _hud.PlaceFriendRequested+=PlaceFriend;
        _hud.OpenWorkerRequested+=OpenWorker;
        _hud.WorkerAssignChestRequested+=AssignWorkerChest;
        _hud.WorkerPriorityRequested+=CycleWorkerPriority;
        _hud.WorkerRadiusRequested+=SetWorkerRadius;
        _hud.WorkerProtectionRequested+=ToggleWorkerProtection;
        _hud.WorkerRecallRequested+=RecallWorker;
        _hud.WorkerStopRequested+=StopWorker;
        _hud.MapTravelRequested+=QueueMapTravel;
        _hud.RetryLoadRequested+=()=>Callable.From(()=>{_savePath=_originalPath; LoadSession();}).CallDeferred();
        _hud.SeparateWorldRequested+=()=>Callable.From(StartSeparateWorld).CallDeferred();
        _hud.QuitRequested+=RequestQuit;
        _harvest.StatusChanged+=_hud.SetInteractionText;
        _harvest.Changed+=message=>{Changed(); _hud.ShowNotification(message);};
        _harvest.OpenEntityRequested+=OpenEntity;
        _harvest.OpenResidentRequested+=OpenResident;
        _harvest.SpecialChestRequested+=ClaimSpecialRewardWorld;
        _harvest.TravelRequested+=QueueTravel;
        _building.StatusMessage+=_hud.ShowNotification;
        _building.Changed+=Changed;
        _building.SelectionChanged+=_hud.SetBuildSelection;
        LoadSession();
        GD.Print($"Bear Adventure build {GameHud.BuildId}; save schema {GameSaveData.CurrentFormatVersion}; Godot {Engine.GetVersionInfo()["string"]}");
    }
    private string SelectedSavePath()
    {
        try
        {
            if(IOFile.Exists(_selectorPath))
            {
                string name=IOFile.ReadAllText(_selectorPath).Trim();
                if(name==Path.GetFileName(name) && name.StartsWith("bear-adventure-recovery-world-",StringComparison.Ordinal) && name.EndsWith(".json",StringComparison.Ordinal))
                {
                    string path=Path.Combine(Path.GetDirectoryName(_originalPath)!,name);
                    if(IOFile.Exists(path)) return path;
                }
            }
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException) { GD.PushWarning(e.Message); }
        return _originalPath;
    }
    private void LoadSession()
    {
        _input.SetLoading(true); _saveAllowed=false;
        try
        {
            var result=_saves.LoadSession(_savePath);
            if(result.Status==SaveLoadStatus.Failed)
            { _hud.ShowLoadError(result.Message); return; }
            var candidate=result.Session ?? GameSessionState.CreateDevelopmentStarter(DefaultSeed);
            StartSession(candidate);
            _saveAllowed=true;
            _hud.CloseForTransition();
            _input.SetLoading(false);
            _hud.ShowNotification($"Build {GameHud.BuildId}. "+result.Message);
            GD.Print("Save path: "+_savePath);
            if(_savePath==_originalPath && IOFile.Exists(_selectorPath)) IOFile.Delete(_selectorPath);
        }
        catch(Exception e)
        { GD.PushError(e.ToString()); _hud.ShowLoadError("World could not be prepared. Existing saves were not changed.\n"+e.Message); }
    }
    private void StartSession(GameSessionState candidate)
    {
        var d=candidate.GetDefinition(candidate.CurrentIslandId,_generator);
        ResidentService.EnsureForIsland(candidate,d);
        var state=candidate.GetIslandState(candidate.CurrentIslandId);
        var queries=new WorldQueries(d,state);
        var spawn=queries.SafeSpawn();
        if(candidate.PlayerLocation is { } location && location.IslandId==candidate.CurrentIslandId && queries.FreeForBear(new(location.X,location.Y)))
            spawn=new(location.X,location.Y);
        var staged=new IslandView(d,state,candidate.Residents) {Name=$"Island_{d.IslandId}"};
        try { staged.PreparePhysics(); AddChild(staged); }
        catch { if(staged.GetParent() is not null) RemoveChild(staged); staged.QueueFree(); throw; }
        var previous=_island;
        _session=candidate; _island=staged;
        BindWorld(staged,spawn);
        if(previous is not null) { RemoveChild(previous); previous.QueueFree(); }
        _dirty=false; _autosave=0; _saveDelay=-1;
        UpdateHud(); UpdateMap();
    }
    private void BindWorld(IslandView view,WorldPoint spawn)
    {
        _bear.SafeRespawn=()=>view.GetSuggestedSpawnPosition();
        _bear.ConfigureIsland(view.LandLeftX,view.LandRightX,view.GetSuggestedSpawnPosition());
        _bear.ConfigureCamera(0,view.WorldWidth,view.WorldTop,view.WorldBottom);
        _bear.PlaceAt(IslandView.Vector(spawn));
        _bear.ClimbEnabled=view.Queries.IsLadder(spawn);
        _harvest.Configure(_bear,view,_session!.Inventory,_input.State,_session);
        _building.Configure(_bear,view,_session.Inventory,_input.State);
        _hud.ConfigureInventory(_session.Inventory);
        _hud.SetBuildSelection(_building.SelectedItem,_building.Layer);
        _backdrop.Configure(view.Definition.Biome,view.Definition.IslandId);
        RenderingServer.SetDefaultClearColor(view.SkyColor);
    }
    private void StartSeparateWorld()
    {
        _input.SetLoading(true); _saveAllowed=false;
        try
        {
            _savePath=SaveGameStore.NewSeparateWorldPath(_originalPath);
            StartSession(GameSessionState.CreateDevelopmentStarter(DefaultSeed));
            _saveAllowed=true; _dirty=true;
            if(!SaveSession()) { _saveAllowed=false; _hud.ShowLoadError("Could not save the separate world. Original files remain untouched."); return; }
            IOFile.WriteAllText(_selectorPath,Path.GetFileName(_savePath));
            _hud.CloseForTransition(); _input.SetLoading(false);
            _hud.ShowNotification("Separate recovery world created. Original save and backup were left untouched.");
            GD.Print("Separate world: "+_savePath);
        }
        catch(Exception e) { _hud.ShowLoadError(e.Message); GD.PushError(e.ToString()); }
    }
    private void CancelTransientActions()
    { _harvest?.CancelActions(); _building?.CancelActions(); _bear?.CancelMotion(); }
    public override void _PhysicsProcess(double delta)
    {
        if(_input is null || _input.BlocksWorld || _session is null || _island is null) return;
        _bear.ClimbEnabled=_island.Queries.IsLadder(IslandView.Point(_bear.Position));
        bool changed=_simulation.Advance(_session,new WorldSeed(_session.Seed),_generator,delta);
        _dirty=true;
        if(changed) { _island.QueueRedraw(); UpdateHud(); }
        _autosave+=delta;
        if(_autosave>=10) { _autosave=0; SaveSession(); }
    }
    public override void _Process(double delta)
    {
        if(_bear is not null && _backdrop is not null)
        {
            var camera=_bear.GetNodeOrNull<Camera2D>("Camera");
            if(camera is not null) _backdrop.SetCameraX(camera.GetScreenCenterPosition().X);
        }
        // Saving a completed UI transaction is allowed while simulation is paused.
        if(_saveDelay>=0)
        {
            _saveDelay-=delta;
            if(_saveDelay<0 && _dirty) SaveSession();
        }
        if(_session is not null && _island is not null && _hud.Context is { } context &&
            !WorldActions.ValidateEntity(_island.Queries,_session.CurrentIslandId,IslandView.Point(_bear.Position),context,out _,out _,out _))
        { _hud.HidePanels(); _hud.ShowNotification("That object is no longer in reach."); }
        if(_session is not null && _island is not null && _hud.TradeOpen && _hud.ResidentContext is { } residentId &&
            !ResidentService.CanInteract(_session,_island.Definition,_session.CurrentIslandId,IslandView.Point(_bear.Position),residentId,out _,out _))
        { _hud.HidePanels(); _hud.ShowNotification("That resident is no longer in reach."); }
        if(_session is not null && _island is not null && _hud.WorkerOpen && _hud.ResidentContext is { } workerResidentId &&
            !ResidentService.CanInteract(_session,_island.Definition,_session.CurrentIslandId,IslandView.Point(_bear.Position),workerResidentId,out _,out _))
        { _hud.HidePanels(); _hud.ShowNotification("That worker is no longer in reach."); }
    }
    private void Changed()
    { _dirty=true; _saveDelay=0.75; _hud.RefreshInventory(); _hud.RefreshResidents(); UpdateHud(); }
    private void Craft(string id)
    {
        if(_session is null || !_hud.CraftingOpen) return;
        var recipe=CraftingCatalog.Find(id);
        if(recipe is null) return;
        if(!InventoryTransactions.TryExchange(_session.Inventory,recipe.Cost,recipe.Result,1,out var reason))
        { _hud.ShowNotification(reason); return; }
        Changed(); _hud.ShowNotification("Crafted "+recipe.DisplayName+".");
    }
    private void CraftAtStation(WorldEntityRef id,string recipeId,ItemType? flower)
    {
        if(_session is null || _island is null || !_hud.StationOpen || _hud.Context!=id) return;
        if(WorldActions.TryStation(_island.Queries,_session.CurrentIslandId,_session.Inventory,IslandView.Point(_bear.Position),id,recipeId,flower,out var reason))
        { Changed(); _hud.ShowNotification("Recipe completed."); } else _hud.ShowNotification(reason);
    }
    private void OpenEntity(WorldEntityRef id)
    {
        if(_session is null || _island is null || _input.BlocksWorld) return;
        var actor=IslandView.Point(_bear.Position);
        if(!WorldActions.ValidateEntity(_island.Queries,_session.CurrentIslandId,actor,id,out var placed,out var chest,out var reason))
        { _hud.ShowNotification(reason); return; }
        if(chest is not null || placed?.Item==ItemType.Chest)
        {
            if(!WorldActions.TryStorage(_island.Queries,_session.CurrentIslandId,actor,id,out var contents,out bool takeOnly,out reason)) return;
            _hud.OpenStorage(id,contents!,takeOnly); _dirty=true; _saveDelay=0.75;
        }
        else if(placed is not null) _hud.OpenStation(id,placed.Item);
    }
    private void Transfer(WorldEntityRef id,bool deposit,ItemType? item,int amount)
    {
        if(_session is null || _island is null || !_hud.StorageOpen || _hud.Context!=id) return;
        if(WorldActions.TryTransfer(_island.Queries,_session.CurrentIslandId,_session.Inventory,IslandView.Point(_bear.Position),id,deposit,item,amount,out var reason))
        { Changed(); _island.QueueRedraw(); } else _hud.ShowNotification(reason);
    }
    private void OpenResident(int residentId)
    {
        if(_session is null || _island is null || _input.BlocksWorld) return;
        if(!ResidentService.CanInteract(_session,_island.Definition,_session.CurrentIslandId,IslandView.Point(_bear.Position),residentId,out var resident,out var reason) || resident is null)
        { _hud.ShowNotification(reason); return; }
        _hud.OpenResident(resident);
    }

    private void TradeWithResident(int residentId,string offerId)
    {
        if(_session is null || _island is null || !_hud.TradeOpen || _hud.ResidentContext!=residentId) return;
        if(!ResidentService.CanInteract(_session,_island.Definition,_session.CurrentIslandId,IslandView.Point(_bear.Position),residentId,out var resident,out var reason) || resident is null)
        { _hud.HidePanels(); _hud.ShowNotification(reason); return; }
        if(ResidentTradingService.TryTrade(resident,_session.Inventory,offerId,out reason))
        { Changed(); _island.QueueRedraw(); _hud.ShowNotification(reason); }
        else _hud.ShowNotification(reason);
    }

    private void InviteResident(int residentId)
    {
        if(_session is null || _island is null || !_hud.TradeOpen || _hud.ResidentContext!=residentId) return;
        if(ResidentService.TryMoveToRoster(_session,_island.Definition,_session.CurrentIslandId,IslandView.Point(_bear.Position),residentId,out var reason))
        { _hud.HidePanels(); Changed(); _island.QueueRedraw(); _hud.ShowNotification(reason); UpdateMap(); }
        else _hud.ShowNotification(reason);
    }

    private void ClaimSpecialRewardWorld(int residentId)
    {
        if(_session is null || _island is null || _input.BlocksWorld) return;
        if(!ResidentService.CanInteract(_session,_island.Definition,_session.CurrentIslandId,IslandView.Point(_bear.Position),residentId,out var resident,out var reason) || resident is null)
        { _hud.ShowNotification(reason); return; }
        if(ResidentTradingService.TryClaimSpecialReward(resident,_session.Inventory,out reason))
        { Changed(); _island.QueueRedraw(); _hud.ShowNotification(reason); }
        else _hud.ShowNotification(reason);
    }

    private void ClaimSpecialReward(int residentId)
    {
        if(_session is null || _island is null || !_hud.TradeOpen || _hud.ResidentContext!=residentId) return;
        if(!ResidentService.CanInteract(_session,_island.Definition,_session.CurrentIslandId,IslandView.Point(_bear.Position),residentId,out var resident,out var reason) || resident is null)
        { _hud.ShowNotification(reason); return; }
        if(ResidentTradingService.TryClaimSpecialReward(resident,_session.Inventory,out reason))
        { Changed(); _island.QueueRedraw(); _hud.ShowNotification(reason); }
        else _hud.ShowNotification(reason);
    }

    private void PlaceFriend(int residentId)
    {
        if(_session is null || _island is null || !_hud.FriendsOpen) return;
        if(ResidentService.TryPlaceFromRoster(_session,_island.Queries,IslandView.Point(_bear.Position),residentId,out var reason))
        { Changed(); _island.QueueRedraw(); _hud.ShowNotification(reason); UpdateMap(); }
        else _hud.ShowNotification(reason);
    }

    private void OpenWorker(int residentId)
    {
        if(_session is null || _island is null || !_hud.TradeOpen || _hud.ResidentContext!=residentId) return;
        if(!ResidentService.CanInteract(_session,_island.Definition,_session.CurrentIslandId,IslandView.Point(_bear.Position),residentId,out var resident,out var reason) || resident is null)
        { _hud.ShowNotification(reason); return; }
        if(!WorkerService.CanWork(resident)) { _hud.ShowNotification("Reach 100 friendship with an ordinary bear first."); return; }
        var chests=_island.State.PlacedObjects.Where(p=>p.Item==ItemType.Chest).OrderBy(p=>p.PlacementId).ToArray();
        _hud.OpenWorker(resident,chests);
    }

    private void AssignWorkerChest(int residentId,int chestPlacementId)
    {
        if(_session is null || _island is null || !_hud.WorkerOpen || _hud.ResidentContext!=residentId) return;
        if(WorkerService.TryAssign(_session,_island.Queries,residentId,chestPlacementId,out var reason))
        { Changed(); _island.QueueRedraw(); _hud.ShowNotification(reason); _hud.RefreshResidents(); UpdateMap(); }
        else _hud.ShowNotification(reason);
    }

    private void CycleWorkerPriority(int residentId,WorkerJob job)
    {
        if(_session is null || !_hud.WorkerOpen || _hud.ResidentContext!=residentId) return;
        if(WorkerService.TryCyclePriority(_session,residentId,job,out var reason))
        { Changed(); _hud.ShowNotification(reason); _hud.RefreshResidents(); }
        else _hud.ShowNotification(reason);
    }

    private void SetWorkerRadius(int residentId,int radius)
    {
        if(_session is null || !_hud.WorkerOpen || _hud.ResidentContext!=residentId) return;
        if(WorkerService.TrySetRadius(_session,residentId,radius,out var reason))
        { Changed(); _hud.ShowNotification(reason); _hud.RefreshResidents(); }
        else _hud.ShowNotification(reason);
    }

    private void ToggleWorkerProtection(int residentId)
    {
        if(_session is null || !_hud.WorkerOpen || _hud.ResidentContext!=residentId) return;
        if(WorkerService.TryToggleProtection(_session,residentId,out var reason))
        { Changed(); _hud.ShowNotification(reason); _hud.RefreshResidents(); }
        else _hud.ShowNotification(reason);
    }

    private void StopWorker(int residentId)
    {
        if(_session is null || !_hud.WorkerOpen || _hud.ResidentContext!=residentId) return;
        if(WorkerService.TryStop(_session,residentId,out var reason))
        { Changed(); _island?.QueueRedraw(); _hud.ShowNotification(reason); _hud.RefreshResidents(); UpdateMap(); }
        else _hud.ShowNotification(reason);
    }

    private void RecallWorker(int residentId)
    {
        if(_session is null || !_hud.WorkerOpen || _hud.ResidentContext!=residentId) return;
        if(WorkerService.TryRecall(_session,residentId,out var reason))
        { _hud.HidePanels(); Changed(); _island?.QueueRedraw(); _hud.ShowNotification(reason); UpdateMap(); }
        else _hud.ShowNotification(reason);
    }

    private void QueueTravel(BoatSide side)
    {
        if(_session is null || _island is null || _input.BlocksWorld) return;
        _input.SetTransition(true);
        Callable.From(()=>Travel(side)).CallDeferred();
    }
    private void Travel(BoatSide side)
    {
        IslandView? staged=null;
        var origin=_island;
        var originPosition=_bear.Position;
        bool committed=false;
        try
        {
            if(_session is null || origin is null) return;
            // Revalidate both ID and physical departure range even if a stale callback was queued.
            var actor=IslandView.Point(_bear.Position);
            var shore=IslandView.Point(origin.GetBoatSiteCenter(side));
            var boat=WorldGrid.BoatHull(origin.Definition.WidthCells,side).Center;
            if(!origin.Queries.InReach(actor,shore,WorldActions.InteractionRange) && !origin.Queries.InReach(actor,boat,WorldActions.InteractionRange))
                throw new InvalidOperationException("Departure point is out of reach.");
            var prepared=TravelService.Prepare(_session,side,id=>_generator.Generate(new WorldSeed(_session.Seed),id));
            staged=new IslandView(prepared.Definition,prepared.State,_session.Residents) {Name=$"Island_{prepared.DestinationId}"};
            staged.PreparePhysics(); staged.RefreshBoatCollision(prepared.ArrivalSide); AddChild(staged);
            // All preparation/physics setup succeeds before the source scene or session is removed.
            BindWorld(staged,prepared.Spawn);
            TravelService.Commit(_session,prepared);
            committed=true;
            ResidentService.EnsureForIsland(_session,prepared.Definition);
            _island=staged; staged=null;
            _hud.CloseForTransition();
            RemoveChild(origin); origin.QueueFree();
            Changed(); UpdateMap(); SaveSession();
            _hud.ShowNotification($"Arrived on island {_session.CurrentIslandId}.");
        }
        catch(Exception e)
        {
            if(!committed)
            {
                if(staged is not null) { if(staged.GetParent() is not null) RemoveChild(staged); staged.QueueFree(); }
                if(origin is not null && _session is not null) { _island=origin; BindWorld(origin,IslandView.Point(originPosition)); }
                _hud.ShowNotification("Travel cancelled; origin retained. "+e.Message);
            }
            else
            {
                // Never roll back only the scene after a successful domain commit.
                // Post-commit presentation errors retain the complete destination session.
                if(origin is not null && GodotObject.IsInstanceValid(origin) && origin.GetParent()==this)
                { RemoveChild(origin); origin.QueueFree(); }
                _dirty=true; _saveDelay=0;
                _hud.ShowNotification("Arrived, but the interface could not refresh: "+e.Message);
            }
            GD.PushError(e.ToString());
        }
        finally { _input.SetTransition(false); }
    }
    private void UpdateHud()
    {
        if(_session is not null && _island is not null)
            _hud.SetIslandInfo(_session.Seed,_island.Definition,_island.RemainingNaturalFeatureCount);
    }
    private IReadOnlyList<DiscoveryMapEntry> BuildMapEntries()
    {
        if(_session is null || _island is null) return Array.Empty<DiscoveryMapEntry>();
        return DiscoveryMapService.Build(_session,_generator,CurrentDockInReach());
    }

    private BoatSide? CurrentDockInReach()
    {
        if(_session is null || _island is null || !GodotObject.IsInstanceValid(_bear)) return null;
        WorldPoint actor=IslandView.Point(_bear.Position);
        BoatSide? best=null;
        double bestDistance=double.PositiveInfinity;

        foreach(var side in Enum.GetValues<BoatSide>())
        {
            if(!_island.State.IsBoatBuilt(side)) continue;
            WorldPoint shore=IslandView.Point(_island.GetBoatSiteCenter(side));
            WorldPoint hull=WorldGrid.BoatHull(_island.Definition.WidthCells,side).Center;
            double distance=Math.Min(actor.DistanceSquared(shore),actor.DistanceSquared(hull));
            if(distance<=WorldActions.InteractionRange*WorldActions.InteractionRange && distance<bestDistance)
            { best=side; bestDistance=distance; }
        }
        return best;
    }

    private void UpdateMap()
    {
        // The map is provider-driven so source-dock range and production status are
        // evaluated at the moment it is rendered. SetMapText remains a cheap refresh hook.
        _hud.SetMapText(string.Empty);
    }

    private void QueueMapTravel(int destinationId)
    {
        if(_session is null || _island is null || !_hud.AnyPanelOpen) return;
        _input.SetTransition(true);
        Callable.From(()=>MapTravel(destinationId)).CallDeferred();
    }

    private void MapTravel(int destinationId)
    {
        IslandView? staged=null;
        IslandView? origin=_island;
        Vector2 originPosition=_bear.Position;
        bool committed=false;

        try
        {
            if(_session is null || origin is null) return;
            BoatSide? departure=CurrentDockInReach();
            if(!departure.HasValue) throw new InvalidOperationException("Stand beside a completed source dock.");

            var entries=DiscoveryMapService.Build(_session,_generator,departure);
            DiscoveryMapEntry? entry=entries.FirstOrDefault(e=>e.IslandId==destinationId);
            if(entry is null || !entry.CanTravel || !entry.ArrivalSide.HasValue)
                throw new InvalidOperationException(entry?.TravelReason ?? "Destination is not available.");

            var prepared=TravelService.PrepareDirect(
                _session,departure.Value,destinationId,entry.ArrivalSide.Value,
                id=>_generator.Generate(new WorldSeed(_session.Seed),id));

            staged=new IslandView(prepared.Definition,prepared.State,_session.Residents)
            { Name=$"Island_{prepared.DestinationId}" };
            staged.PreparePhysics();
            AddChild(staged);

            BindWorld(staged,prepared.Spawn);
            TravelService.Commit(_session,prepared);
            committed=true;
            ResidentService.EnsureForIsland(_session,prepared.Definition);
            _island=staged; staged=null;
            _hud.CloseForTransition();
            RemoveChild(origin); origin.QueueFree();
            Changed(); UpdateMap(); SaveSession();
            _hud.ShowNotification($"Travelled to island {_session.CurrentIslandId}.");
        }
        catch(Exception e)
        {
            if(!committed)
            {
                if(staged is not null)
                { if(staged.GetParent() is not null) RemoveChild(staged); staged.QueueFree(); }
                if(origin is not null && _session is not null)
                { _island=origin; BindWorld(origin,IslandView.Point(originPosition)); }
                _hud.ShowNotification("Map travel cancelled; origin retained. "+e.Message);
            }
            else
            {
                if(origin is not null && GodotObject.IsInstanceValid(origin) && origin.GetParent()==this)
                { RemoveChild(origin); origin.QueueFree(); }
                _dirty=true; _saveDelay=0;
                _hud.ShowNotification("Arrived, but the interface could not refresh: "+e.Message);
            }
            GD.PushError(e.ToString());
        }
        finally { _input.SetTransition(false); }
    }

    private bool SaveSession()
    {
        if(!_saveAllowed || _session is null) return false;
        try
        {
            if(GodotObject.IsInstanceValid(_bear))
                _session.PlayerLocation=new(_session.CurrentIslandId,_bear.Position.X,_bear.Position.Y);
            _saves.Save(_savePath,GameSaveData.FromSession(_session)); _dirty=false; _saveDelay=-1; return true;
        }
        catch(Exception e)
        {
            GD.PushError("Save failed; previous files retained. "+e);
            if(GodotObject.IsInstanceValid(_hud)) _hud.ShowNotification("SAVE FAILED — previous save retained. See Godot output.");
            _saveDelay=-1; return false;
        }
    }
    private void RequestQuit()
    {
        if(_quitting) return;
        if(_saveAllowed && !SaveSession()) { _hud.ShowPause("Save failed. Resume and retry; previous save was retained."); return; }
        _quitting=true; GetTree().Quit();
    }
    public override void _Notification(int what)
    { if(what==NotificationWMCloseRequest && _hud is not null) RequestQuit(); }
    public override void _ExitTree()
    { if(!_quitting && _saveAllowed) SaveSession(); }
}
