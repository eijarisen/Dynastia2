using BearAdventure.Domain.Gameplay;
using BearAdventure.Domain.World;
using BearAdventure.Interaction;
using BearAdventure.Rendering.PixelArt;
using Godot;

namespace BearAdventure.UI;

public partial class GameHud : CanvasLayer
{
    public const string BuildId = "20-21.1";
    private enum WindowKind { None, Inventory, Crafting, Map, Station, Storage, Trade, Friends, Worker, Pause, Error }
    private WindowKind _window;
    private Control _root=null!;
    private ColorRect _veil=null!, _panel=null!;
    private Label _title=null!, _island=null!, _bag=null!, _selection=null!, _prompt=null!, _notice=null!, _build=null!;
    private Control _body=null!;
    private InventoryState? _inventory, _storage;
    private bool _takeOnly;
    private ItemType _station;
    private ItemType? _flower;
    private WorldEntityRef? _context;
    private string _mapText="",_error="";
    private double _noticeSeconds;
    private int? _residentId;
    private IReadOnlyList<PlacedObjectState> _workerChests=Array.Empty<PlacedObjectState>();
    private Func<IReadOnlyList<ResidentState>>? _residentProvider;
    private Func<IReadOnlyList<DiscoveryMapEntry>>? _mapProvider;
    public bool AnyPanelOpen => _window != WindowKind.None;
    public WorldEntityRef? Context => _context;
    public bool CraftingOpen => _window==WindowKind.Crafting;
    public bool StationOpen => _window==WindowKind.Station;
    public bool StorageOpen => _window==WindowKind.Storage;
    public bool TradeOpen => _window==WindowKind.Trade;
    public bool FriendsOpen => _window==WindowKind.Friends;
    public bool WorkerOpen => _window==WindowKind.Worker;
    public int? ResidentContext => _residentId;
    public event Action<bool>? ModalChanged;
    public event Action<ItemType>? PlaceableItemSelected;
    public event Action<string>? CraftRequested;
    public event Action<WorldEntityRef,string,ItemType?>? StationCraftRequested;
    public event Action<WorldEntityRef,bool,ItemType?,int>? TransferRequested;
    public event Action<int,string>? TradeRequested;
    public event Action<int>? InviteResidentRequested;
    public event Action<int>? SpecialRewardRequested;
    public event Action<int>? PlaceFriendRequested;
    public event Action<int>? OpenWorkerRequested;
    public event Action<int,int>? WorkerAssignChestRequested;
    public event Action<int,WorkerJob>? WorkerPriorityRequested;
    public event Action<int,int>? WorkerRadiusRequested;
    public event Action<int>? WorkerProtectionRequested;
    public event Action<int>? WorkerRecallRequested;
    public event Action<int>? WorkerStopRequested;
    public event Action<int>? MapTravelRequested;
    public event Action? RetryLoadRequested;
    public event Action? SeparateWorldRequested;
    public event Action? QuitRequested;

    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always; Layer=50;
        _root=new Control {MouseFilter=Control.MouseFilterEnum.Ignore}; AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _island=Text(_root,"",new(22,20),new(560,72),16);
        _bag=Text(_root,"",new(22,98),new(950,32),16);
        _build=Text(_root,$"BEAR ADVENTURE  |  BUILD {BuildId}",new(920,20),new(340,34),14);
        _selection=Text(_root,"BUILD: none",new(22,136),new(1160,28),15);
        _notice=Text(_root,"",new(200,188),new(880,58),17); _notice.HorizontalAlignment=HorizontalAlignment.Center;
        _prompt=Text(_root,"",new(30,546),new(1220,68),16); _prompt.HorizontalAlignment=HorizontalAlignment.Center;
        Text(_root,"A/D walk · W/Space jump · W/S ladder · Hold F harvest / hovered mining · R talk/use · G fish\nE inventory · C craft · M map · T friends · Esc pause/close · Q layer · Left-drag build (or mine) · Right-drag remove",
            new(24,640),new(1220,60),14);
        _veil=new ColorRect {Color=new(0,0,0,0.65f),MouseFilter=Control.MouseFilterEnum.Stop,Visible=false};
        _root.AddChild(_veil); _veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel=new ColorRect {Position=new(120,120),Size=new(1040,490),MouseFilter=Control.MouseFilterEnum.Stop}; _veil.AddChild(_panel);
        _title=Text(_panel,"",new(24,18),new(920,36),24);
        PixelUi.Decorate(_panel);
        var close=Button(_panel,"Close [Esc]",new(884,16),new(134,36),HidePanels);
        close.FocusMode=Control.FocusModeEnum.None;
        _body=new Control {Position=new(20,64),Size=new(1000,406),MouseFilter=Control.MouseFilterEnum.Ignore}; _panel.AddChild(_body);
        PixelUi.StyleLabels(_root); RefreshInventory();
    }
    public override void _Process(double delta)
    {
        if(_noticeSeconds>0 && (_noticeSeconds-=delta)<=0) _notice.Text="";
        // The project uses a 1280x720 logical viewport; canvas_items handles physical window scaling.
    }
    public void ConfigureInventory(InventoryState bag) { _inventory=bag; RefreshInventory(); }
    public void HandleMenu(MenuCommand command)
    {
        if(_window==WindowKind.Error) return;
        if(command==MenuCommand.Escape)
        { if(AnyPanelOpen) HidePanels(); else ShowPause(); return; }
        WindowKind requested=command switch {MenuCommand.Inventory=>WindowKind.Inventory,MenuCommand.Crafting=>WindowKind.Crafting,
            MenuCommand.Friends=>WindowKind.Friends,_=>WindowKind.Map};
        if(_window==requested) HidePanels(); else Open(requested);
    }
    private void Open(WindowKind kind)
    {
        if(kind is not WindowKind.Station and not WindowKind.Storage) _context=null;
        if(kind is not WindowKind.Trade and not WindowKind.Worker) _residentId=null;
        _window=kind; _veil.Visible=true;
        ModalChanged?.Invoke(true); RenderWindow();
    }
    public void HidePanels()
    {
        if(_window==WindowKind.Error) return; // Only retry/separate-world/quit resolves a failed load.
        _window=WindowKind.None; _context=null; _residentId=null; _storage=null; _workerChests=Array.Empty<PlacedObjectState>(); _veil.Visible=false;
        GetViewport().GuiReleaseFocus(); ModalChanged?.Invoke(false);
    }
    public void CloseForTransition()
    { _window=WindowKind.None; _context=null; _residentId=null; _storage=null; _workerChests=Array.Empty<PlacedObjectState>(); _veil.Visible=false; ModalChanged?.Invoke(false); }
    public void ShowPause(string message="Simulation paused.") { _error=message; Open(WindowKind.Pause); }
    public void ShowLoadError(string message) { _error=message; Open(WindowKind.Error); }
    public void OpenStation(WorldEntityRef id,ItemType station)
    { _context=id; _station=station; if(!_flower.HasValue || !(_inventory?.Has(_flower.Value)??false)) _flower=null; Open(WindowKind.Station); }
    public void OpenStorage(WorldEntityRef id,InventoryState contents,bool takeOnly)
    { _context=id; _storage=contents; _takeOnly=takeOnly; Open(WindowKind.Storage); }
    public void ConfigureResidentProvider(Func<IReadOnlyList<ResidentState>> provider) { _residentProvider=provider; }
    public void ConfigureMapProvider(Func<IReadOnlyList<DiscoveryMapEntry>> provider) { _mapProvider=provider; }
    public void OpenResident(ResidentState resident) { _residentId=resident.ResidentId; Open(WindowKind.Trade); }
    public void OpenWorker(ResidentState resident,IReadOnlyList<PlacedObjectState> chests)
    { _residentId=resident.ResidentId; _workerChests=chests; Open(WindowKind.Worker); }
    public void RefreshResidents() { if(_window is WindowKind.Trade or WindowKind.Friends or WindowKind.Worker) RenderWindow(); }
    public void SetMapText(string text) { _mapText=text; if(_window==WindowKind.Map) RenderWindow(); }
    public void SetIslandInfo(string seed,IslandDefinition d,int remaining) =>
        _island.Text=$"Island {d.IslandId} · {d.Biome} · {d.WidthCells} cells · {remaining} resources\nSeed: {seed}";
    public void SetInteractionText(string value) => _prompt.Text=value;
    public void SetBuildSelection(ItemType? item,BuildLayer layer) => _selection.Text=item is null ? "BUILD: none · left mouse mines hovered terrain" :
        $"BUILD: {PlacementRules.GetDisplayName(item.Value)} [{layer}] · Q layer · right-click empty space to deselect";
    public void ShowNotification(string text) { _notice.Text=text; _noticeSeconds=5; }
    public void RefreshInventory()
    {
        if(_inventory is null || _bag is null) return;
        _bag.Text=$"Wood {_inventory.Get(ItemType.Wood)}   Stone {_inventory.Get(ItemType.Stone)}   Ore {_inventory.Get(ItemType.IronOre)}   Bars {_inventory.Get(ItemType.IronBar)}   Honey {_inventory.Get(ItemType.Honey)}   Gold {_inventory.Get(ItemType.GoldCoin)}   Fish {_inventory.Get(ItemType.Fish)}";
        if(AnyPanelOpen) RenderWindow();
    }
    private void RenderWindow()
    {
        if(_body is null) return;
        foreach(Node node in _body.GetChildren()) { _body.RemoveChild(node); node.QueueFree(); }
        _title.Text=_window switch {WindowKind.Storage=>_takeOnly?"GENERATED LOOT — TAKE ONLY":"CHEST STORAGE",WindowKind.Station=>_station.ToString().ToUpperInvariant(),
            WindowKind.Trade=>"RESIDENT",WindowKind.Friends=>"FRIENDS ROSTER",WindowKind.Worker=>"WORKER SETTINGS",WindowKind.Error=>"SAVE COULD NOT BE LOADED",WindowKind.Pause=>"PAUSED",_=>_window.ToString().ToUpperInvariant()};
        switch(_window)
        {
            case WindowKind.Inventory: RenderInventory(); break;
            case WindowKind.Crafting: RenderCrafting(); break;
            case WindowKind.Station: RenderStation(); break;
            case WindowKind.Storage: RenderStorage(); break;
            case WindowKind.Trade: RenderTrade(); break;
            case WindowKind.Friends: RenderFriends(); break;
            case WindowKind.Worker: RenderWorker(); break;
            case WindowKind.Map: RenderMap(); break;
            case WindowKind.Pause:
                Text(_body,_error+"\nGrowth, honey, movement and world edits are stopped.",new(12,20),new(940,100),18);
                Button(_body,"Resume",new(280,155),new(430,58),HidePanels);
                Button(_body,"Save and quit",new(280,233),new(430,58),()=>QuitRequested?.Invoke()); break;
            case WindowKind.Error:
                Text(_body,_error,new(8,0),new(970,155),16);
                Button(_body,"Retry the original save",new(120,178),new(760,52),()=>RetryLoadRequested?.Invoke());
                Button(_body,"Play a separate world (leave original files untouched)",new(120,246),new(760,52),()=>SeparateWorldRequested?.Invoke());
                Button(_body,"Quit without saving",new(120,314),new(760,52),()=>QuitRequested?.Invoke()); break;
        }
        PixelUi.StyleLabels(_body);
    }
    private void RenderMap()
    {
        var entries=_mapProvider?.Invoke() ?? Array.Empty<DiscoveryMapEntry>();
        Text(_body,"View discovered islands anywhere. Direct travel requires standing beside a completed source dock and a completed destination dock.",
            new(4,0),new(980,44),14);
        var list=Scroll(_body,new(4,50),new(984,352));
        foreach(var entry in entries)
        {
            string docks=$"{(entry.LeftDock?"L":"-")}/{(entry.RightDock?"R":"-")}";
            string workers=entry.WorkerCount==0?"no workers":
                entry.BlockedWorkerCount>0?$"{entry.WorkerCount} worker(s), {entry.BlockedWorkerCount} blocked":$"{entry.WorkerCount} worker(s)";
            string honey=entry.ReadyHoney>0?$"{entry.ReadyHoney} honey ready":
                entry.InsufficientHiveCount>0?$"{entry.InsufficientHiveCount} hive(s) need flowers":
                entry.FullHiveCount>0?$"{entry.FullHiveCount} full hive(s)":"production normal";
            string summary=$"Island {entry.IslandId} · {entry.Biome} · docks {docks}"+
                (entry.IsCurrent?" · HERE":"")+
                $"\nResidents {entry.ResidentCount} · {workers} · chests {entry.PlayerChestCount} · structures {entry.StructureCount} · {honey}";

            var card=new VBoxContainer {CustomMinimumSize=new(0,94)};
            list.AddChild(card);
            card.AddChild(new Label {Text=summary,AutowrapMode=TextServer.AutowrapMode.WordSmart});
            var travel=new Button {Text=entry.CanTravel?"Travel":"Travel unavailable — "+entry.TravelReason,
                Disabled=!entry.CanTravel,CustomMinimumSize=new(0,34),FocusMode=Control.FocusModeEnum.None};
            card.AddChild(travel);
            if(entry.CanTravel)
            {
                int target=entry.IslandId;
                travel.Pressed+=()=>MapTravelRequested?.Invoke(target);
            }
        }
        if(entries.Count==0) list.AddChild(new Label {Text="No discovered islands."});
    }

    private void RenderInventory()
    {
        if(_inventory is null) return;
        var list=Scroll(_body,new(4,0),new(984,402));
        foreach(var (item,count) in _inventory.Counts.OrderBy(e=>e.Key))
        {
            var button=ListButton(list,$"{PlacementRules.GetDisplayName(item)}  ×{count}"+(PlacementRules.IsPlaceable(item)?"   · select to build":""),PixelAtlas.Icon(item));
            button.Disabled=!PlacementRules.IsPlaceable(item);
            button.Pressed+=()=>{PlaceableItemSelected?.Invoke(item); HidePanels();};
        }
        if(_inventory.IsEmpty) list.AddChild(new Label {Text="Inventory is empty."});
    }
    private void RenderCrafting()
    {
        if(_inventory is null) return;
        var list=Scroll(_body,new(4,0),new(984,402));
        foreach(var recipe in CraftingCatalog.All)
        {
            var b=ListButton(list,recipe.DisplayName+"   · "+CraftingService.DescribeCost(recipe),PixelAtlas.Icon(recipe.Result.First().Key));
            b.Disabled=!CraftingService.CanCraft(_inventory,recipe);
            b.Pressed+=()=>CraftRequested?.Invoke(recipe.Id);
        }
    }
    private void RenderStation()
    {
        if(_inventory is null || !_context.HasValue) return;
        var context=_context.Value;
        int top=0;
        if(_station==ItemType.Cauldron)
        {
            Text(_body,"Choose the exact flower to consume:",new(4,0),new(970,28),16);
            var flowers=new HBoxContainer {Position=new(4,32),Size=new(970,58)}; _body.AddChild(flowers);
            foreach(var item in Enum.GetValues<ItemType>().Where(PlacementRules.IsFlower))
            {
                var flower=item;
                var b=new Button {Text=$"{flower.ToString().Replace("Flower","")} ({_inventory.Get(flower)})",Disabled=!_inventory.Has(flower),
                    ToggleMode=true,ButtonPressed=_flower==flower,FocusMode=Control.FocusModeEnum.None};
                flowers.AddChild(b); b.Pressed+=()=>{_flower=flower; RenderWindow();};
            }
            top=104;
        }
        var list=Scroll(_body,new(4,top),new(984,402-top));
        foreach(var recipe in StationCraftingService.GetRecipes(_station))
        {
            var b=ListButton(list,recipe.DisplayName+"   · "+recipe.CostDescription,PixelUi.StationIcon(recipe.Id));
            b.Disabled=!StationCraftingService.CanCraft(_inventory,_station,recipe.Id,_flower);
            b.Pressed+=()=>StationCraftRequested?.Invoke(context,recipe.Id,_flower);
        }
    }
    private ResidentState? CurrentResident() => _residentId.HasValue
        ? _residentProvider?.Invoke().FirstOrDefault(r=>r.ResidentId==_residentId.Value) : null;

    private void RenderTrade()
    {
        if(_inventory is null) return;
        var resident=CurrentResident();
        if(resident is null) { Text(_body,"Resident is no longer available.",new(8,8),new(960,60),18); return; }
        string relationship=resident.Kind is ResidentKind.PolarBear or ResidentKind.Monkey ? "Trust" : "Friendship";
        string wallet=resident.Kind==ResidentKind.Bear ? $"   Coins {resident.Coins}/{resident.MaxCoins}" : "";
        Text(_body,$"{resident.Name} · {resident.Kind} · {relationship} {resident.Sympathy}/100{wallet}",new(4,0),new(970,34),18);
        var list=Scroll(_body,new(4,42),new(984,278));
        foreach(var offer in ResidentTradingService.GetOffers(resident))
        {
            string cost=string.Join(", ",offer.Cost.Select(p=>$"{p.Value} {PlacementRules.GetDisplayName(p.Key)}"));
            string result=string.Join(", ",offer.Result.Select(p=>$"{p.Value} {PlacementRules.GetDisplayName(p.Key)}"));
            bool available=ResidentTradingService.CanTrade(resident,_inventory,offer.Id,out var blockedReason);
            var b=ListButton(list,$"{offer.DisplayName}\n{cost}  →  {result}"+(available?"":$"   · {blockedReason}"),PixelAtlas.Icon(offer.Result.First().Key));
            b.Disabled=!available;
            string id=offer.Id; int residentId=resident.ResidentId; b.Pressed+=()=>TradeRequested?.Invoke(residentId,id);
        }
        int buttonY=330;
        if(resident.Relocatable && resident.Status==ResidentStatus.World && resident.Befriended)
            Button(_body,$"Invite {resident.Name} to friends",new(60,buttonY),new(360,52),()=>InviteResidentRequested?.Invoke(resident.ResidentId));
        if(resident.Kind==ResidentKind.Bear && resident.Status==ResidentStatus.World && resident.Befriended)
            Button(_body,resident.Worker?.Assigned==true?"Worker settings":"Assign as worker",new(520,buttonY),new(360,52),()=>OpenWorkerRequested?.Invoke(resident.ResidentId));
        if(resident.Kind is ResidentKind.PolarBear or ResidentKind.Monkey)
        {
            string chest=resident.Kind==ResidentKind.PolarBear?"Ice chest":"Golden chest";
            var b=Button(_body,resident.SpecialChestClaimed?chest+" — empty":chest+" — open at 100 trust",new(520,buttonY),new(380,52),()=>SpecialRewardRequested?.Invoke(resident.ResidentId));
            b.Disabled=resident.SpecialChestClaimed || resident.Sympathy<100;
        }
    }

    private void RenderFriends()
    {
        var residents=_residentProvider?.Invoke() ?? Array.Empty<ResidentState>();
        Text(_body,"Befriended bears and cats keep their identity, friendship and origin. Place them on the current island when ready.",new(4,0),new(970,52),15);
        var list=Scroll(_body,new(4,60),new(984,340));
        var roster=residents.Where(r=>r.Status==ResidentStatus.Roster).OrderBy(r=>r.Name).ToArray();
        foreach(var resident in roster)
        {
            string workerCargo=resident.Worker is { } w && !w.Cargo.IsEmpty ? " · cargo " + string.Join(", ",w.Cargo.Counts.Select(p=>$"{p.Value} {PlacementRules.GetDisplayName(p.Key)}")) : "";
            var b=ListButton(list,$"{resident.Name} · {resident.Kind} · friendship {resident.Sympathy}/100 · from island {resident.OriginIslandId}{workerCargo}",
                PixelAtlas.Get(resident.Kind==ResidentKind.Cat?"resident/cat":"bear/idle/0"));
            int residentId=resident.ResidentId; b.Pressed+=()=>PlaceFriendRequested?.Invoke(residentId);
        }
        if(roster.Length==0) list.AddChild(new Label {Text="No friends are waiting in the roster. Reach 100 friendship with an ordinary bear or cat, then invite them."});
    }

    private void RenderWorker()
    {
        var resident=CurrentResident();
        if(resident is null || resident.Kind!=ResidentKind.Bear || !resident.Befriended)
        { Text(_body,"Worker is no longer available.",new(8,8),new(960,60),18); return; }
        var worker=resident.Worker;
        Text(_body,$"{resident.Name} · {WorkerService.Describe(resident)}",new(4,0),new(970,46),16);
        int y=52;
        Text(_body,"Home chest",new(4,y),new(200,28),16); y+=30;
        var chestRow=new HBoxContainer {Position=new(4,y),Size=new(972,46)}; _body.AddChild(chestRow);
        foreach(var chest in _workerChests.OrderBy(c=>c.PlacementId))
        {
            int chestId=chest.PlacementId; bool selected=worker?.HomeChestPlacementId==chestId;
            var b=new Button {Text=$"#{chestId} @ {chest.CellX}"+(selected?" ✓":""),CustomMinimumSize=new(150,40),FocusMode=Control.FocusModeEnum.None};
            chestRow.AddChild(b); b.Pressed+=()=>WorkerAssignChestRequested?.Invoke(resident.ResidentId,chestId);
        }
        if(_workerChests.Count==0) chestRow.AddChild(new Label {Text="Place a player chest first."});
        y+=54;
        if(worker is null) { Text(_body,"Select a chest to create this worker assignment.",new(4,y),new(970,50),16); return; }
        Text(_body,"Job priorities — click to cycle Off → Low → Normal → High",new(4,y),new(970,28),15); y+=30;
        var jobs=new GridContainer {Columns=4,Position=new(4,y),Size=new(970,112)}; _body.AddChild(jobs);
        foreach(var job in Enum.GetValues<WorkerJob>())
        {
            var captured=job; int priority=worker.Priority(job);
            var b=new Button {Text=$"{WorkerService.Display(job)}\n{WorkerService.PriorityName(priority)}",CustomMinimumSize=new(230,50),FocusMode=Control.FocusModeEnum.None};
            jobs.AddChild(b); b.Pressed+=()=>WorkerPriorityRequested?.Invoke(resident.ResidentId,captured);
        }
        y+=122;
        Text(_body,"Work radius",new(4,y),new(150,28),15);
        int rx=150;
        foreach(int radius in new[]{12,24,48,96})
        {
            int captured=radius; var b=Button(_body,$"{radius}"+(worker.WorkRadiusCells==radius?" ✓":""),new(rx,y-6),new(86,36),()=>WorkerRadiusRequested?.Invoke(resident.ResidentId,captured)); rx+=94;
        }
        var protect=Button(_body,worker.ProtectHiveFlowers?"Hive flowers protected ✓":"Hive flowers can be gathered",new(535,y-6),new(435,36),()=>WorkerProtectionRequested?.Invoke(resident.ResidentId));
        y+=48;
        Button(_body,"Stop work",new(110,y),new(300,46),()=>WorkerStopRequested?.Invoke(resident.ResidentId));
        Button(_body,"Recall to friends — cargo preserved",new(500,y),new(390,46),()=>WorkerRecallRequested?.Invoke(resident.ResidentId));
    }

    private void RenderStorage()
    {
        if(_inventory is null || _storage is null || !_context.HasValue) return;
        var context=_context.Value;
        Text(_body,"No slot limit. Stack button transfers up to 99 units; All transfers that item.",new(4,0),new(978,28),15);
        Text(_body,_takeOnly?"CHEST LOOT":"CHEST CONTENTS",new(4,38),new(460,26),16);
        Text(_body,"PLAYER BAG",new(506,38),new(460,26),16);
        var take=Button(_body,"Withdraw all",new(4,70),new(475,38),()=>TransferRequested?.Invoke(context,false,null,int.MaxValue)); take.Disabled=_storage.IsEmpty;
        var deposit=Button(_body,"Deposit all",new(506,70),new(475,38),()=>TransferRequested?.Invoke(context,true,null,int.MaxValue)); deposit.Disabled=_takeOnly || _inventory.IsEmpty;
        StorageColumn(Scroll(_body,new(4,118),new(478,284)),_storage,false,context);
        StorageColumn(Scroll(_body,new(506,118),new(478,284)),_inventory,true,context);
    }
    private void StorageColumn(VBoxContainer list,InventoryState source,bool deposit,WorldEntityRef context)
    {
        foreach(var (item,count) in source.Counts.OrderBy(e=>e.Key))
        {
            var row=new VBoxContainer(); list.AddChild(row);
            row.AddChild(new Label {Text=$"{PlacementRules.GetDisplayName(item)}   ×{count}"});
            var buttons=new HBoxContainer(); row.AddChild(buttons);
            foreach(int amount in new[] {1,InventoryTransactions.TransferStackSize,int.MaxValue})
            {
                int captured=amount; var capturedItem=item;
                var b=new Button {Text=amount==int.MaxValue?"All":amount==1?"1":"Stack (99)",CustomMinimumSize=new(120,32),
                    Disabled=deposit && _takeOnly,FocusMode=Control.FocusModeEnum.None}; buttons.AddChild(b);
                b.Pressed+=()=>TransferRequested?.Invoke(context,deposit,capturedItem,captured);
            }
        }
        if(source.IsEmpty) list.AddChild(new Label {Text="Empty"});
    }
    private static Label Text(Node parent,string text,Vector2 position,Vector2 size,int font)
    {
        var label=new Label {Text=text,Position=position,Size=size,AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=Control.MouseFilterEnum.Ignore};
        label.AddThemeFontSizeOverride("font_size",font); parent.AddChild(label); return label;
    }
    private static Button Button(Node parent,string text,Vector2 position,Vector2 size,Action action)
    {
        var b=new Button {Text=text,Position=position,Size=size,FocusMode=Control.FocusModeEnum.None}; parent.AddChild(b); b.Pressed+=action; return b;
    }
    private static VBoxContainer Scroll(Node parent,Vector2 position,Vector2 size)
    {
        var scroll=new ScrollContainer {Position=position,Size=size,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled}; parent.AddChild(scroll);
        var list=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,CustomMinimumSize=new(size.X-20,0)}; scroll.AddChild(list); return list;
    }
    private static Button ListButton(VBoxContainer list,string text,Texture2D icon)
    {
        var b=new Button {Text=text,CustomMinimumSize=new(0,58),FocusMode=Control.FocusModeEnum.None};
        PixelUi.StyleButton(b,icon); list.AddChild(b); return b;
    }
}
