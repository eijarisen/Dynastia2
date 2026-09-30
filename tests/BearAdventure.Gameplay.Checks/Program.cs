using System.Text.Json;
using System.Diagnostics;
using BearAdventure.Domain.Gameplay;
using BearAdventure.Domain.Simulation;
using BearAdventure.Domain.World;
using BearAdventure.Persistence;

int passed=0,failed=0;
void Test(string name, Action body)
{
    try { body(); passed++; Console.WriteLine("PASS "+name); }
    catch(Exception e) { failed++; Console.Error.WriteLine("FAIL "+name+": "+e); }
}
void Assert(bool value,string message="Assertion failed") { if(!value) throw new Exception(message); }
void Throws<T>(Action action) where T:Exception
{ try { action(); } catch(T) { return; } throw new Exception("Expected "+typeof(T).Name); }
IslandDefinition Flat(int id=0, IEnumerable<NaturalFeatureSpawn>? features=null, BiomeType biome=BiomeType.Forest,
    IEnumerable<GeneratedStructureDefinition>? structures=null, int[]? heights=null)
{
    var open=new HashSet<UndergroundCell>();
    for(int x=38;x<40;x++) for(int l=0;l>=-100;l--) open.Add(new(x,l));
    return new() { IslandId=id,IslandSeed=(ulong)(100+id),GeneratorVersion=1,Biome=biome,WidthCells=80,BoatSiteWidthCells=10,
        SurfaceLevels=heights ?? new int[80],NaturalFeatures=features?.ToArray()??Array.Empty<NaturalFeatureSpawn>(),
        UndergroundOpenCells=open,UndergroundOres=new Dictionary<UndergroundCell,UndergroundOreSpawn>(),MineShaftLeftCell=38,
        MineShaftWidthCells=2,GeneratedStructures=structures?.ToArray()??Array.Empty<GeneratedStructureDefinition>() };
}
GameSessionState Session(IslandDefinition d, IslandDeltaState? state=null)
{
    var session=new GameSessionState(new InventoryState(),"test-seed-12-14");
    session.SetBaseline(d); session.SetIslandState(d.IslandId,state??new()); session.CurrentIslandId=d.IslandId; return session;
}
void Advance(GameSessionState session,double seconds) => new WorldSimulationService().Advance(session,new WorldSeed(session.Seed),new IslandGenerator(),seconds);
GameSessionState Clone(GameSessionState session) => JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(GameSaveData.FromSession(session)))!.ToSession();
WorldQueries Query(GameSessionState session,int id=0) => new(session.GetDefinition(id,new IslandGenerator()),session.GetIslandState(id));
GameSessionState HoneyWorld()
{
    var d=Flat(features:new[] {new NaturalFeatureSpawn(1,18,0,NaturalFeatureKind.Flower,0),new(2,19,0,NaturalFeatureKind.Flower,1),new(3,21,0,NaturalFeatureKind.Flower,2)});
    var s=Session(d); s.GetIslandState(0).AddPlacedObject(ItemType.Beehive,20,1,BuildLayer.Solid); return s;
}

Test("negative inventory rejected before mutation",()=> {
    var i=new InventoryState(); i.Set(ItemType.Wood,5);
    Throws<ArgumentOutOfRangeException>(()=>i.Set(ItemType.Wood,-1)); Assert(i.Get(ItemType.Wood)==5);
});
Test("inventory overflow is checked",()=> {
    var i=new InventoryState(); i.Set(ItemType.Wood,int.MaxValue);
    Throws<OverflowException>(()=>i.Add(ItemType.Wood,1)); Assert(i.Get(ItemType.Wood)==int.MaxValue);
});
Test("replace validates every entry first",()=> {
    var i=new InventoryState(); i.Set(ItemType.Wood,5);
    Throws<ArgumentException>(()=>i.ReplaceWith(new Dictionary<ItemType,int> {[ItemType.Stone]=4,[ItemType.Wood]=-2}));
    Assert(i.Get(ItemType.Wood)==5 && i.Get(ItemType.Stone)==0);
});
Test("single transfer conserves totals",()=> {
    var a=new InventoryState(); var b=new InventoryState(); a.Set(ItemType.Wood,9);
    Assert(InventoryTransactions.TryTransfer(a,b,ItemType.Wood,1,out _)); Assert(a.Get(ItemType.Wood)==8 && b.Get(ItemType.Wood)==1);
});
Test("failed bulk transfer is all-or-nothing",()=> {
    var a=new InventoryState(); var b=new InventoryState(); a.Set(ItemType.Wood,9); a.Set(ItemType.Stone,3); b.Set(ItemType.Stone,int.MaxValue);
    Assert(!InventoryTransactions.TryTransfer(a,b,a.Snapshot(),out _)); Assert(a.Get(ItemType.Wood)==9 && b.Get(ItemType.Wood)==0);
});
Test("zero and negative transfers are rejected",()=> {
    var a=new InventoryState(); var b=new InventoryState(); a.Set(ItemType.Wood,9);
    Assert(!InventoryTransactions.TryTransfer(a,b,ItemType.Wood,0,out _)); Assert(!InventoryTransactions.TryTransfer(a,b,ItemType.Wood,-1,out _)); Assert(a.Get(ItemType.Wood)==9);
});
Test("same-inventory transfer is rejected",()=> {
    var a=new InventoryState(); a.Set(ItemType.Wood,4); Assert(!InventoryTransactions.TryTransfer(a,a,ItemType.Wood,2,out _)); Assert(a.Get(ItemType.Wood)==4);
});
Test("repeated clicks cannot overdraw",()=> {
    var a=new InventoryState(); var b=new InventoryState(); a.Set(ItemType.Wood,5);
    int successes=0; for(int n=0;n<20;n++) if(InventoryTransactions.TryTransfer(a,b,ItemType.Wood,1,out _)) successes++;
    Assert(successes==5 && a.IsEmpty && b.Get(ItemType.Wood)==5);
});
Test("craft overflow does not consume input",()=> {
    var bag=new InventoryState(); bag.Set(ItemType.Wood,30); bag.Set(ItemType.Beehive,int.MaxValue);
    Assert(!CraftingService.TryCraft(bag,CraftingCatalog.Find("beehive")!)); Assert(bag.Get(ItemType.Wood)==30);
});
Test("normal craft uses original recipe",()=> {
    var bag=new InventoryState(); bag.Set(ItemType.Wood,30);
    Assert(CraftingService.TryCraft(bag,CraftingCatalog.Find("beehive")!)); Assert(bag.Get(ItemType.Wood)==0 && bag.Get(ItemType.Beehive)==1);
});
Test("cooking consumes selected flower only",()=> {
    var bag=new InventoryState(); bag.Set(ItemType.Cactus,1); bag.Set(ItemType.PinkFlower,1); bag.Set(ItemType.RedFlower,1);
    Assert(StationCraftingService.TryCraft(bag,ItemType.Cauldron,"juice",ItemType.RedFlower));
    Assert(bag.Get(ItemType.PinkFlower)==1 && bag.Get(ItemType.RedFlower)==0 && bag.Get(ItemType.Juice)==1);
});
Test("cooking requires explicit flower",()=> {
    var bag=new InventoryState(); bag.Set(ItemType.Cactus,1); bag.Set(ItemType.RedFlower,1);
    Assert(!StationCraftingService.TryCraft(bag,ItemType.Cauldron,"juice")); Assert(bag.Get(ItemType.Cactus)==1);
});
Test("stale station ID cannot craft",()=> {
    var s=Session(Flat()); s.Inventory.Set(ItemType.Wood,1); s.Inventory.Set(ItemType.IronOre,1);
    Assert(!WorldActions.TryStation(Query(s),0,s.Inventory,WorldGrid.Center(20,1),new(0,WorldEntityKind.Placed,999),"iron-bar",null,out _));
    Assert(s.Inventory.Get(ItemType.Wood)==1);
});
Test("station removed after opening cannot craft",()=> {
    var s=Session(Flat()); var st=s.GetIslandState(0); var forge=st.AddPlacedObject(ItemType.Forge,20,1,BuildLayer.Solid);
    st.RemovePlacedObject(forge.PlacementId); s.Inventory.Set(ItemType.Wood,1); s.Inventory.Set(ItemType.IronOre,1);
    Assert(!WorldActions.TryStation(Query(s),0,s.Inventory,WorldGrid.Center(19,1),new(0,WorldEntityKind.Placed,forge.PlacementId),"iron-bar",null,out _));
});
Test("foreign island cannot reuse station ID",()=> {
    var s=Session(Flat()); var forge=s.GetIslandState(0).AddPlacedObject(ItemType.Forge,20,1,BuildLayer.Solid);
    s.Inventory.Set(ItemType.Wood,1); s.Inventory.Set(ItemType.IronOre,1);
    Assert(!WorldActions.TryStation(Query(s),1,s.Inventory,WorldGrid.Center(19,1),new(0,WorldEntityKind.Placed,forge.PlacementId),"iron-bar",null,out _));
});
Test("out-of-range station cannot craft",()=> {
    var s=Session(Flat()); var forge=s.GetIslandState(0).AddPlacedObject(ItemType.Forge,20,1,BuildLayer.Solid);
    s.Inventory.Set(ItemType.Wood,1); s.Inventory.Set(ItemType.IronOre,1);
    Assert(!WorldActions.TryStation(Query(s),0,s.Inventory,WorldGrid.Center(40,1),new(0,WorldEntityKind.Placed,forge.PlacementId),"iron-bar",null,out _));
});
Test("valid forge action is transactional",()=> {
    var s=Session(Flat()); var forge=s.GetIslandState(0).AddPlacedObject(ItemType.Forge,20,1,BuildLayer.Solid);
    s.Inventory.Set(ItemType.Wood,1); s.Inventory.Set(ItemType.IronOre,1);
    Assert(WorldActions.TryStation(Query(s),0,s.Inventory,WorldGrid.Center(19,1),new(0,WorldEntityKind.Placed,forge.PlacementId),"iron-bar",null,out _));
    Assert(s.Inventory.Get(ItemType.IronBar)==1 && s.Inventory.Get(ItemType.IronOre)==0);
});
Test("two chests retain independent storage across reload",()=> {
    var s=Session(Flat()); var a=s.GetIslandState(0).AddPlacedObject(ItemType.Chest,20,1,BuildLayer.Solid);
    var b=s.GetIslandState(0).AddPlacedObject(ItemType.Chest,24,1,BuildLayer.Solid);
    a.Contents.Set(ItemType.Wood,27); b.Contents.Set(ItemType.Stone,63);
    var copy=Clone(s); var items=copy.GetIslandState(0).PlacedObjects;
    Assert(items[0].Contents.Get(ItemType.Wood)==27 && items[1].Contents.Get(ItemType.Wood)==0 && items[1].Contents.Get(ItemType.Stone)==63);
});
Test("full chest cannot be dismantled",()=> {
    var s=Session(Flat()); var c=s.GetIslandState(0).AddPlacedObject(ItemType.Chest,20,1,BuildLayer.Solid); c.Contents.Set(ItemType.Wood,10);
    Assert(!WorldActions.TryRemove(Query(s),s.Inventory,WorldGrid.Center(19,1),c.PlacementId,out var why));
    Assert(why.Contains("Empty") && s.GetIslandState(0).PlacedObjects.Count==1 && c.Contents.Get(ItemType.Wood)==10);
});
Test("empty chest is recovered once",()=> {
    var s=Session(Flat()); var c=s.GetIslandState(0).AddPlacedObject(ItemType.Chest,20,1,BuildLayer.Solid);
    Assert(WorldActions.TryRemove(Query(s),s.Inventory,WorldGrid.Center(19,1),c.PlacementId,out _));
    Assert(!WorldActions.TryRemove(Query(s),s.Inventory,WorldGrid.Center(19,1),c.PlacementId,out _)); Assert(s.Inventory.Get(ItemType.Chest)==1);
});
Test("placement IDs are not reused after reload",()=> {
    var s=Session(Flat()); var st=s.GetIslandState(0); var first=st.AddPlacedObject(ItemType.Chest,20,1,BuildLayer.Solid); st.RemovePlacedObject(first.PlacementId);
    var copy=Clone(s); Assert(copy.GetIslandState(0).AddPlacedObject(ItemType.Chest,21,1,BuildLayer.Solid).PlacementId>first.PlacementId);
});
Test("deposit all and withdraw stack preserve counts",()=> {
    var s=Session(Flat()); var c=s.GetIslandState(0).AddPlacedObject(ItemType.Chest,20,1,BuildLayer.Solid); var ctx=new WorldEntityRef(0,WorldEntityKind.Placed,c.PlacementId);
    s.Inventory.Set(ItemType.Wood,140); s.Inventory.Set(ItemType.Stone,6);
    Assert(WorldActions.TryTransfer(Query(s),0,s.Inventory,WorldGrid.Center(19,1),ctx,true,null,int.MaxValue,out _));
    Assert(s.Inventory.IsEmpty && c.Contents.Get(ItemType.Wood)==140);
    Assert(WorldActions.TryTransfer(Query(s),0,s.Inventory,WorldGrid.Center(19,1),ctx,false,ItemType.Wood,99,out _));
    Assert(s.Inventory.Get(ItemType.Wood)==99 && c.Contents.Get(ItemType.Wood)==41);
});
Test("generated chest partial loot does not reroll",()=> {
    var chest=new GeneratedChestDefinition(1000,20,1,new Dictionary<ItemType,int>{[ItemType.Wood]=12,[ItemType.Honey]=3});
    var s=Session(Flat(structures:new[]{new GeneratedStructureDefinition(0,GeneratedStructureKind.House,20,6,0,chest)}));
    var id=new WorldEntityRef(0,WorldEntityKind.GeneratedChest,1000);
    Assert(WorldActions.TryTransfer(Query(s),0,s.Inventory,WorldGrid.Center(19,1),id,false,ItemType.Wood,1,out _));
    var copy=Clone(s); var c=copy.GetIslandState(0).OpenGeneratedChest(chest);
    Assert(c.Get(ItemType.Wood)==11 && c.Get(ItemType.Honey)==3 && copy.Inventory.Get(ItemType.Wood)==1);
    Assert(!WorldActions.TryTransfer(Query(copy),0,copy.Inventory,WorldGrid.Center(19,1),id,true,ItemType.Wood,1,out _));
});
Test("legacy emptied chest remains empty",()=> {
    var chest=new GeneratedChestDefinition(1000,20,1,new Dictionary<ItemType,int>{[ItemType.Wood]=12});
    var state=new IslandDeltaState(); state.MarkGeneratedChestLooted(1000); Assert(state.OpenGeneratedChest(chest).IsEmpty);
});
Test("cell boundaries are consistent at positive and negative levels",()=> {
    for(int l=-100;l<=100;l++)
    {
        var rect=WorldGrid.CellRect(new(20,l));
        Assert(WorldGrid.CellAt(new(rect.X,rect.Y))==new UndergroundCell(20,l));
        Assert(WorldGrid.CellAt(new(rect.Right-0.001,rect.Bottom-0.001))==new UndergroundCell(20,l));
    }
    Assert(WorldGrid.CellAt(new(WorldGrid.LandLeft,0)).LogicalLevel==0);
});
Test("excavated room supports chest placement",()=> {
    var s=Session(Flat()); var st=s.GetIslandState(0);
    for(int x=19;x<=22;x++) for(int l=-3;l>=-5;l--) st.MarkUndergroundCellMined(new(x,l));
    s.Inventory.Set(ItemType.Chest,1);
    Assert(WorldActions.TryPlace(Query(s),s.Inventory,WorldGrid.Center(22,-4),ItemType.Chest,new(20,-5),BuildLayer.Solid,out var c,out var why),why);
    Assert(c is not null && s.Inventory.Get(ItemType.Chest)==0);
});
Test("bedrock is not fertile soil",()=> {
    var s=Session(Flat()); s.GetIslandState(0).MarkUndergroundCellMined(new(20,-5));
    Assert(!Query(s).CanPlace(ItemType.Sapling,new(20,-5),BuildLayer.Solid,WorldGrid.BearRect(WorldGrid.Center(22,-4)),out _));
});
Test("bottom row building agrees with the outside-world foundation",()=> {
    var s=Session(Flat()); var st=s.GetIslandState(0);
    for(int x=19;x<=22;x++) for(int l=-97;l>=-100;l--) st.MarkUndergroundCellMined(new(x,l));
    Assert(Query(s).CanPlace(ItemType.Chest,new(20,-100),BuildLayer.Solid,WorldGrid.BearRect(WorldGrid.Center(22,-98)),out var why),why);
    Assert(!Query(s).CanMine(new(20,-101),out _));
});
Test("background wall cannot support a solid station",()=> {
    var s=Session(Flat()); s.GetIslandState(0).AddPlacedObject(ItemType.Wood,20,3,BuildLayer.Background);
    Assert(!Query(s).CanPlace(ItemType.Forge,new(20,4),BuildLayer.Solid,WorldGrid.BearRect(WorldGrid.Center(22,3)),out _));
});
Test("mining a chest foundation is refused",()=> {
    var s=Session(Flat()); s.GetIslandState(0).AddPlacedObject(ItemType.Chest,20,1,BuildLayer.Solid);
    Assert(!Query(s).CanMine(new(20,0),out _));
});
Test("shore access stays reserved",()=> {
    var s=Session(Flat()); Assert(!Query(s).CanMine(new(0,0),out _));
    Assert(!Query(s).CanPlace(ItemType.Wood,new(1,1),BuildLayer.Solid,WorldGrid.BearRect(WorldGrid.Center(5,1)),out _));
});
Test("mine ladder access stays reserved",()=> {
    var s=Session(Flat()); Assert(!Query(s).CanPlace(ItemType.Wood,new(38,-5),BuildLayer.Solid,WorldGrid.BearRect(WorldGrid.Center(37,-5)),out _));
});
Test("mining cannot pay twice",()=> {
    var s=Session(Flat()); var actor=WorldGrid.Center(20,1);
    Assert(WorldActions.TryMine(Query(s),s.Inventory,actor,new(20,0),out _));
    Assert(!WorldActions.TryMine(Query(s),s.Inventory,actor,new(20,0),out _)); Assert(s.Inventory.Get(ItemType.Stone)==1);
});
Test("higher-than-zero mined cell persists",()=> {
    var heights=new int[80]; heights[20]=5; var s=Session(Flat(heights:heights));
    s.GetIslandState(0).MarkUndergroundCellMined(new(20,5)); var copy=Clone(s);
    Assert(!Query(copy).TerrainSolid(new(20,5)));
});
Test("all 201 logical rows stay addressable",()=> {
    var q=Query(Session(Flat())); Assert(q.InBounds(new(20,-100)) && q.InBounds(new(20,100)));
    Assert(!q.InBounds(new(20,-101)) && !q.InBounds(new(20,101)));
});
Test("boat hulls are completely over water",()=> {
    foreach(var side in Enum.GetValues<BoatSide>())
    {
        var hull=WorldGrid.BoatHull(80,side);
        Assert(side==BoatSide.Left?hull.Right<WorldGrid.LandLeft:hull.X>WorldGrid.LandLeft+80*48);
    }
});
Test("invalid saved player position has legal fallback",()=> {
    var q=Query(Session(Flat())); Assert(!q.FreeForBear(new(WorldGrid.LandLeft+20*48,600)));
    Assert(q.FreeForBear(q.SafeSpawn()));
});
Test("normal small and large tree yields remain distinct",()=> {
    var bag=new InventoryState(); var small=HarvestRules.GetRewards(new(1,20,0,NaturalFeatureKind.Tree,0),bag);
    var large=HarvestRules.GetRewards(new(2,21,0,NaturalFeatureKind.Tree,0),bag);
    Assert(small.Single(r=>r.Item==ItemType.Wood).Amount==1 && large.Single(r=>r.Item==ItemType.Wood).Amount==2);
    Assert(large.Single(r=>r.Item==ItemType.Sapling).Amount==1);
});
Test("diamond tree multiplier is retained",()=> {
    var bag=new InventoryState(); bag.Set(ItemType.DiamondAxe,1);
    var loot=HarvestRules.GetRewards(new(2,21,0,NaturalFeatureKind.Tree,0),bag);
    Assert(loot.Single(r=>r.Item==ItemType.Wood).Amount==4 && loot.Single(r=>r.Item==ItemType.Sapling).Amount==2);
});
Test("iron pickaxe guarantees surface iron",()=> {
    var bag=new InventoryState(); bag.Set(ItemType.IronPickaxe,1);
    for(int n=0;n<32;n++) Assert(HarvestRules.GetRewards(new(n,20,0,NaturalFeatureKind.Rock,n%3),bag).Any(r=>r.Item==ItemType.IronOre));
});
Test("a mature planted tree uses the shared harvest operation",()=> {
    var s=Session(Flat()); var st=s.GetIslandState(0); var tree=st.AddPlacedObject(ItemType.Sapling,20,1,BuildLayer.Solid); tree.GrowthSeconds=60;
    Assert(WorldActions.TryHarvestPlanted(Query(s),s.Inventory,WorldGrid.Center(21,1),tree.PlacementId,out _));
    Assert(s.Inventory.Get(ItemType.Wood)>=1 && s.Inventory.Get(ItemType.Sapling)==1 && st.PlacedObjects.Count==0);
});
Test("placed flowers are harvestable",()=> {
    var s=Session(Flat()); var f=s.GetIslandState(0).AddPlacedObject(ItemType.PurpleFlower,20,1,BuildLayer.Solid);
    Assert(WorldActions.TryHarvestPlanted(Query(s),s.Inventory,WorldGrid.Center(21,1),f.PlacementId,out _)); Assert(s.Inventory.Get(ItemType.PurpleFlower)==1);
});
Test("immature sapling cannot be harvested",()=> {
    var s=Session(Flat()); var tree=s.GetIslandState(0).AddPlacedObject(ItemType.Sapling,20,1,BuildLayer.Solid);
    Assert(!WorldActions.TryHarvestPlanted(Query(s),s.Inventory,WorldGrid.Center(21,1),tree.PlacementId,out _));
});
Test("regrowth cannot overwrite construction",()=> {
    var s=Session(Flat(features:new[]{new NaturalFeatureSpawn(0,20,0,NaturalFeatureKind.Tree,0)})); var st=s.GetIslandState(0);
    st.MarkNaturalFeatureHarvested(0,1); var block=st.AddPlacedObject(ItemType.Wood,20,1,BuildLayer.Solid);
    Advance(s,30); Assert(st.IsNaturalFeatureHarvested(0) && st.PlacedObjects.Contains(block));
    st.RemovePlacedObject(block.PlacementId); Advance(s,5); Assert(!st.IsNaturalFeatureHarvested(0));
});
Test("destroyed soil prevents regrowth",()=> {
    var s=Session(Flat(features:new[]{new NaturalFeatureSpawn(0,20,0,NaturalFeatureKind.Tree,0)})); var st=s.GetIslandState(0);
    st.MarkNaturalFeatureHarvested(0,1); st.MarkUndergroundCellMined(new(20,0)); Advance(s,10); Assert(st.IsNaturalFeatureHarvested(0));
});
Test("sapling reserves mature footprint",()=> {
    var s=Session(Flat()); s.GetIslandState(0).AddPlacedObject(ItemType.Sapling,20,1,BuildLayer.Solid);
    Assert(!Query(s).CanPlace(ItemType.Wood,new(21,2),BuildLayer.Solid,WorldGrid.BearRect(WorldGrid.Center(24,2)),out _));
});
Test("growth blocked by legacy obstruction waits",()=> {
    var s=Session(Flat()); var st=s.GetIslandState(0); var tree=st.AddPlacedObject(ItemType.Sapling,20,1,BuildLayer.Solid);
    var block=st.AddPlacedObject(ItemType.Wood,21,2,BuildLayer.Solid); Advance(s,70); Assert(tree.GrowthSeconds==0);
    st.RemovePlacedObject(block.PlacementId); Advance(s,60); Assert(tree.GrowthSeconds==60);
});
Test("new saplings use the island species",()=> {
    foreach(var biome in Enum.GetValues<BiomeType>())
    {
        var s=Session(Flat(biome:biome)); s.Inventory.Set(ItemType.Sapling,1);
        Assert(WorldActions.TryPlace(Query(s),s.Inventory,WorldGrid.Center(23,1),ItemType.Sapling,new(20,1),BuildLayer.Solid,out var planted,out var why),why);
        Assert(planted!.PlantKind==WorldQueries.PlantedKind(biome));
    }
});
Test("hive needs only flowers, not grass",()=> {
    var s=Session(Flat(features:new[]{new NaturalFeatureSpawn(0,18,0,NaturalFeatureKind.Grass,0),new(1,19,0,NaturalFeatureKind.Grass,0),new(2,21,0,NaturalFeatureKind.Grass,0)}));
    var hive=s.GetIslandState(0).AddPlacedObject(ItemType.Beehive,20,1,BuildLayer.Solid); Advance(s,60); Assert(hive.StoredOutput==0);
});
Test("honey production advances on schedule",()=> {
    var s=HoneyWorld(); var hive=s.GetIslandState(0).PlacedObjects.Single(); Advance(s,44.5); Assert(hive.StoredOutput==0);
    Advance(s,0.5); Assert(hive.StoredOutput==1 && hive.ProductionSeconds==0);
});
Test("full hive discards hidden production backlog",()=> {
    var s=HoneyWorld(); var hive=s.GetIslandState(0).PlacedObjects.Single(); Advance(s,1000); Assert(hive.StoredOutput==3 && hive.ProductionSeconds==0);
    Assert(WorldActions.TryHoney(Query(s),s.Inventory,WorldGrid.Center(20,1),new(0,WorldEntityKind.Placed,hive.PlacementId),out _));
    Advance(s,44.5); Assert(hive.StoredOutput==0); Advance(s,0.5); Assert(hive.StoredOutput==1);
});
Test("hive honors vertical radius",()=> {
    var heights=new int[80]; foreach(int x in new[]{18,19,21}) heights[x]=12;
    var s=Session(Flat(features:new[]{new NaturalFeatureSpawn(1,18,12,NaturalFeatureKind.Flower,0),new(2,19,12,NaturalFeatureKind.Flower,1),new(3,21,12,NaturalFeatureKind.Flower,2)},heights:heights));
    var hive=s.GetIslandState(0).AddPlacedObject(ItemType.Beehive,20,1,BuildLayer.Solid); Advance(s,60); Assert(hive.StoredOutput==0);
});
Test("batched and frame-sized simulation are equivalent",()=> {
    var a=HoneyWorld(); var b=Clone(a);
    Advance(a,180); for(int n=0;n<10800;n++) Advance(b,1.0/60);
    var ha=a.GetIslandState(0).PlacedObjects.Single(); var hb=b.GetIslandState(0).PlacedObjects.Single();
    Assert(ha.StoredOutput==hb.StoredOutput && Math.Abs(ha.ProductionSeconds-hb.ProductionSeconds)<1e-8 && a.SimulationTick==b.SimulationTick);
});
Test("regrowth boundary does not grant a premature honey tick",()=> {
    var a=HoneyWorld(); a.GetIslandState(0).MarkNaturalFeatureHarvested(1,1);
    var b=Clone(a); Advance(a,46); Advance(b,1); Advance(b,45);
    Assert(a.GetIslandState(0).PlacedObjects.Single().StoredOutput==1);
    Assert(a.GetIslandState(0).PlacedObjects.Single().StoredOutput==b.GetIslandState(0).PlacedObjects.Single().StoredOutput);
});
Test("save reload preserves fractional time without extra payout",()=> {
    var s=HoneyWorld(); Advance(s,21.37); var copy=Clone(s);
    Assert(Math.Abs(s.SimulationSeconds-copy.SimulationSeconds)<1e-8);
    Advance(s,23.63); Advance(copy,23.63); Assert(copy.GetIslandState(0).PlacedObjects.Single().StoredOutput==1);
});
Test("inactive discovered islands continue producing",()=> {
    var s=HoneyWorld(); s.SetBaseline(Flat(1)); s.SetIslandState(1,new()); s.CurrentIslandId=1; Advance(s,45);
    Assert(s.GetIslandState(0).PlacedObjects.Single().StoredOutput==1);
});
Test("undiscovered baselines are not simulated",()=> {
    var s=Session(Flat()); s.SetBaseline(Flat(9)); Advance(s,60); Assert(!s.Islands.ContainsKey(9));
});
Test("no offline or reload payout",()=> {
    var s=HoneyWorld(); Advance(s,12); var copy=Clone(s); Assert(copy.SimulationTick==s.SimulationTick);
    Assert(copy.GetIslandState(0).PlacedObjects.Single().StoredOutput==0);
});
Test("modal input cancellation suppresses held keys",()=> {
    var i=new WorldInputState(); i.ObserveNeutral(true); i.Press(GameAction.Harvest); Assert(i.Held(GameAction.Harvest));
    i.SetBlocked(true); Assert(!i.Held(GameAction.Harvest)); i.SetBlocked(false); i.Press(GameAction.Harvest); Assert(!i.Held(GameAction.Harvest));
    i.ObserveNeutral(true); i.Press(GameAction.Harvest); Assert(i.Held(GameAction.Harvest));
});
Test("release over GUI clears held building",()=> {
    var i=new WorldInputState(); i.Press(GameAction.Build); Assert(i.Held(GameAction.Build)); i.Release(GameAction.Build); Assert(!i.Held(GameAction.Build));
});
Test("focus and travel cancellation reset all actions",()=> {
    var i=new WorldInputState(); foreach(var a in Enum.GetValues<GameAction>()) i.Press(a);
    i.Cancel(); Assert(Enum.GetValues<GameAction>().All(a=>!i.Held(a)));
});
Test("travel preparation failure leaves origin intact",()=> {
    var s=Session(Flat()); s.GetIslandState(0).BuildBoat(BoatSide.Right);
    Throws<InvalidOperationException>(()=>TravelService.Prepare(s,BoatSide.Right,_=>throw new InvalidOperationException("Injected")));
    Assert(s.CurrentIslandId==0 && s.DiscoveredIslandIds.SetEquals(new[]{0}) && !s.Islands.ContainsKey(1));
});
Test("travel only discovers at commit",()=> {
    var s=Session(Flat()); s.GetIslandState(0).BuildBoat(BoatSide.Right);
    var plan=TravelService.Prepare(s,BoatSide.Right,id=>Flat(id)); Assert(s.CurrentIslandId==0 && !s.DiscoveredIslandIds.Contains(1));
    TravelService.Commit(s,plan); Assert(s.CurrentIslandId==1 && s.GetIslandState(1).LeftBoatBuilt && s.PlayerLocation!.IslandId==1);
});
Test("stale travel plan rejected",()=> {
    var s=Session(Flat()); s.GetIslandState(0).BuildBoat(BoatSide.Right); var plan=TravelService.Prepare(s,BoatSide.Right,id=>Flat(id));
    s.CurrentIslandId=2; Throws<InvalidOperationException>(()=>TravelService.Commit(s,plan));
});
Test("materialized terrain survives later generator assumptions",()=> {
    var heights=new int[80]; heights[20]=7; var s=Session(Flat(heights:heights)); var copy=Clone(s);
    Assert(copy.GetDefinition(0,new IslandGenerator()).WidthCells==80 && copy.GetDefinition(0,new IslandGenerator()).SurfaceLevels[20]==7);
});
Test("saved world seed is honored",()=> {
    var s=Session(Flat()); var data=GameSaveData.FromSession(s); data.WorldSeed="another-valid-seed";
    var result=data.ToSession(); Assert(result.Seed=="another-valid-seed");
});
Test("bad enum is rejected rather than dropped",()=> {
    var data=GameSaveData.FromSession(Session(Flat())); data.Inventory["FutureResource"]=4; Throws<InvalidDataException>(()=>data.ToSession());
});
Test("negative save quantity is rejected",()=> {
    var data=GameSaveData.FromSession(Session(Flat())); data.Inventory["Wood"]=-1; Throws<InvalidDataException>(()=>data.ToSession());
});
Test("unsupported schema is rejected",()=> {
    var data=GameSaveData.FromSession(Session(Flat())); data.FormatVersion=99; Throws<InvalidDataException>(()=>data.ToSession());
});
Test("duplicate placement identities are rejected",()=> {
    var s=Session(Flat()); s.GetIslandState(0).AddPlacedObject(ItemType.Chest,20,1,BuildLayer.Solid);
    var data=GameSaveData.FromSession(s); data.Islands[0].PlacedObjects.Add(data.Islands[0].PlacedObjects[0]); Throws<InvalidDataException>(()=>data.ToSession());
});
Test("legacy v1 save migrates storage and layouts",()=> {
    string json=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","legacy-v1.json"));
    var session=JsonSerializer.Deserialize<GameSaveData>(json)!.ToSession();
    Assert(session.Seed=="legacy-fixture-12-14" && session.Inventory.Get(ItemType.Wood)==23);
    Assert(session.GetIslandState(0).PlacedObjects.Single().Contents.IsEmpty && session.Baselines.Count==2);
});
Test("resident generation is deterministic and biome-specific",()=> {
    foreach(var biome in Enum.GetValues<BiomeType>())
    {
        var a=Session(Flat(biome:biome)); var b=Session(Flat(biome:biome));
        var ra=ResidentService.EnsureForIsland(a,a.GetDefinition(0,new IslandGenerator()));
        var rb=ResidentService.EnsureForIsland(b,b.GetDefinition(0,new IslandGenerator()));
        Assert(ra.ResidentId==rb.ResidentId && ra.Name==rb.Name && ra.CellX==rb.CellX);
    }
    var desert=Session(Flat(id:2,biome:BiomeType.Desert)); desert.CurrentIslandId=2;
    Assert(ResidentService.EnsureForIsland(desert,desert.GetDefinition(2,new IslandGenerator())).Kind==ResidentKind.Cat);
});
Test("ordinary bear trade uses wallet and friendship",()=> {
    var bag=new InventoryState(); bag.Set(ItemType.Honey,1);
    var r=new ResidentState(1,"Rowan",ResidentKind.Bear,0,0,20,1,0){Coins=2,MaxCoins=10};
    Assert(ResidentTradingService.TryTrade(r,bag,"honey",out _));
    Assert(bag.Get(ItemType.Honey)==0 && bag.Get(ItemType.GoldCoin)==2 && r.Coins==0 && r.Sympathy==5);
});
Test("broke bear cannot consume player goods",()=> {
    var bag=new InventoryState(); bag.Set(ItemType.Soup,1);
    var r=new ResidentState(1,"Rowan",ResidentKind.Bear,0,0,20,1,0){Coins=0,MaxCoins=10};
    Assert(!ResidentTradingService.TryTrade(r,bag,"soup",out _)); Assert(bag.Get(ItemType.Soup)==1);
});
Test("cat exchange preserves exact flower colors",()=> {
    var bag=new InventoryState(); bag.Set(ItemType.RedFlower,2); bag.Set(ItemType.PinkFlower,4);
    var r=new ResidentState(2,"Poppy",ResidentKind.Cat,0,0,20,1,0);
    Assert(ResidentTradingService.TryTrade(r,bag,"red-yellow",out _));
    Assert(bag.Get(ItemType.RedFlower)==0 && bag.Get(ItemType.YellowFlower)==1 && bag.Get(ItemType.PinkFlower)==4 && r.Sympathy==1);
});
Test("bear wallet regenerates only while simulation advances",()=> {
    var s=Session(Flat()); var r=ResidentService.EnsureForIsland(s,s.GetDefinition(0,new IslandGenerator()));
    r.Coins=0; r.MaxCoins=10; Advance(s,40); Assert(r.Coins==2); var copy=Clone(s); Assert(copy.Residents.Single().Coins==2);
});
Test("rare resident offers persist across save reload",()=> {
    var s=Session(Flat()); var r=ResidentService.EnsureForIsland(s,s.GetDefinition(0,new IslandGenerator()));
    r.OffersDiamondAxe=true; r.OffersDiamondPickaxe=true; var c=Clone(s); var restored=c.Residents.Single();
    Assert(restored.OffersDiamondAxe && restored.OffersDiamondPickaxe);
});
Test("fishing requires rod and actual ocean proximity",()=> {
    var s=Session(Flat()); var q=Query(s); var near=WorldGrid.Feet(0,0); var inland=WorldGrid.Feet(40,0);
    Assert(!FishingService.CanStart(q,near,s.Inventory,out _)); s.Inventory.Set(ItemType.FishingRod,1);
    Assert(FishingService.CanStart(q,near,s.Inventory,out _)); Assert(!FishingService.CanStart(q,inland,s.Inventory,out _));
});
Test("polar and monkey trades progress only their own trust",()=> {
    var bag=new InventoryState(); bag.Set(ItemType.Fish,1); bag.Set(ItemType.Banana,1);
    var polar=new ResidentState(3,"Lumi",ResidentKind.PolarBear,0,0,20,1,0);
    var monkey=new ResidentState(4,"Kiko",ResidentKind.Monkey,0,0,22,1,0);
    Assert(ResidentTradingService.TryTrade(polar,bag,"fish",out _)); Assert(polar.Sympathy==2 && monkey.Sympathy==0);
    Assert(ResidentTradingService.TryTrade(monkey,bag,"banana",out _)); Assert(monkey.Sympathy==2);
});
Test("owner-linked special chest pays once",()=> {
    var bag=new InventoryState(); var polar=new ResidentState(5,"Lumi",ResidentKind.PolarBear,0,0,20,1,0){Sympathy=100};
    Assert(ResidentTradingService.TryClaimSpecialReward(polar,bag,out _)); Assert(bag.Get(ItemType.GoldCoin)==150 && bag.Get(ItemType.Honey)==10);
    Assert(!ResidentTradingService.TryClaimSpecialReward(polar,bag,out _)); Assert(bag.Get(ItemType.GoldCoin)==150);
});
Test("special chest state belongs to its resident owner",()=> {
    var bag=new InventoryState(); var a=new ResidentState(6,"Lumi",ResidentKind.PolarBear,0,0,20,1,0){Sympathy=100};
    var b=new ResidentState(7,"Frost",ResidentKind.PolarBear,1,1,20,1,0){Sympathy=100};
    Assert(ResidentTradingService.TryClaimSpecialReward(a,bag,out _)); Assert(a.SpecialChestClaimed && !b.SpecialChestClaimed);
});
Test("befriended bear relocation preserves identity and friendship",()=> {
    var s=Session(Flat()); var r=ResidentService.EnsureForIsland(s,s.GetDefinition(0,new IslandGenerator())); r.Sympathy=100;
    var actor=ResidentService.Feet(r,s.GetDefinition(0,new IslandGenerator()));
    Assert(ResidentService.TryMoveToRoster(s,s.GetDefinition(0,new IslandGenerator()),0,actor,r.ResidentId,out _));
    int id=r.ResidentId; Assert(r.Status==ResidentStatus.Roster);
    Assert(ResidentService.TryPlaceFromRoster(s,Query(s),WorldGrid.Feet(25,0),id,out _));
    Assert(r.ResidentId==id && r.Sympathy==100 && r.Status==ResidentStatus.World);
});
Test("non-relocatable companions remain in their biome",()=> {
    var s=Session(Flat(id:2,biome:BiomeType.Snowy)); s.CurrentIslandId=2; var r=ResidentService.EnsureForIsland(s,s.GetDefinition(2,new IslandGenerator())); r.Sympathy=100;
    Assert(!ResidentService.TryMoveToRoster(s,s.GetDefinition(2,new IslandGenerator()),2,ResidentService.Feet(r,s.GetDefinition(2,new IslandGenerator())),r.ResidentId,out _));
    Assert(r.Status==ResidentStatus.World);
});
Test("failed friend placement leaves roster intact",()=> {
    var s=Session(Flat()); var r=ResidentService.EnsureForIsland(s,s.GetDefinition(0,new IslandGenerator())); r.Sympathy=100; r.Status=ResidentStatus.Roster;
    // Actor far outside valid world makes every near placement candidate impossible.
    Assert(!ResidentService.TryPlaceFromRoster(s,Query(s),new WorldPoint(-100000,-100000),r.ResidentId,out _));
    Assert(r.Status==ResidentStatus.Roster);
});
Test("schema 2 migration creates persistent resident identities",()=> {
    var s=Session(Flat()); var data=GameSaveData.FromSession(s); data.FormatVersion=2; data.Residents.Clear();
    var migrated=data.ToSession(); Assert(migrated.Residents.Count==1); var round=Clone(migrated); Assert(round.Residents.Single().ResidentId==migrated.Residents.Single().ResidentId);
});
ResidentState WorkerBear(GameSessionState s,int id,int cell=20)
{
    var r=new ResidentState(id,$"Worker{id}",ResidentKind.Bear,0,0,cell,1,0) {Sympathy=100,Coins=10,MaxCoins=10};
    s.AddResident(r); return r;
}
void OnlyPriority(WorkerState worker,WorkerJob job)
{
    foreach(var j in Enum.GetValues<WorkerJob>()) worker.SetPriority(j,j==job?3:0);
}

Test("worker assignment requires befriended bear and player chest",()=> {
    var s=Session(Flat()); var chest=s.GetIslandState(0).AddPlacedObject(ItemType.Chest,25,1,BuildLayer.Solid);
    var bear=WorkerBear(s,9001); bear.Sympathy=99;
    Assert(!WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _));
    bear.Sympathy=100; Assert(WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _));
    Assert(bear.Worker is {Assigned:true} && bear.Worker.HomeChestPlacementId==chest.PlacementId);
});
Test("worker harvests a tree and deposits into assigned chest",()=> {
    var tree=new NaturalFeatureSpawn(100,30,0,NaturalFeatureKind.Tree,0); var s=Session(Flat(features:new[]{tree}));
    var chest=s.GetIslandState(0).AddPlacedObject(ItemType.Chest,25,1,BuildLayer.Solid); var bear=WorkerBear(s,9002,20);
    Assert(WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _)); OnlyPriority(bear.Worker!,WorkerJob.HarvestTrees);
    Advance(s,30); Assert(s.GetIslandState(0).IsNaturalFeatureHarvested(tree.FeatureId));
    Assert(chest.Contents.Get(ItemType.Wood)>=1 && chest.Contents.Get(ItemType.Sapling)>=1 && bear.Worker!.Cargo.IsEmpty);
});
Test("worker continues on a discovered inactive island",()=> {
    var active=Flat(0); var remoteTree=new NaturalFeatureSpawn(150,30,0,NaturalFeatureKind.Tree,0); var remote=Flat(1,new[]{remoteTree});
    var s=Session(active); s.SetBaseline(remote); s.SetIslandState(1,new IslandDeltaState()); s.DiscoverIsland(1); s.CurrentIslandId=0;
    var chest=s.GetIslandState(1).AddPlacedObject(ItemType.Chest,25,1,BuildLayer.Solid);
    var bear=new ResidentState(9050,"Remote",ResidentKind.Bear,1,1,20,1,0){Sympathy=100,Coins=10,MaxCoins=10}; s.AddResident(bear);
    Assert(WorkerService.TryAssign(s,new WorldQueries(remote,s.GetIslandState(1)),bear.ResidentId,chest.PlacementId,out _)); OnlyPriority(bear.Worker!,WorkerJob.HarvestTrees);
    Advance(s,35); Assert(s.CurrentIslandId==0 && s.GetIslandState(1).IsNaturalFeatureHarvested(remoteTree.FeatureId)); Assert(chest.Contents.Get(ItemType.Wood)>=1);
});
Test("worker collects ready honey and deposits it",()=> {
    var s=Session(Flat()); var st=s.GetIslandState(0); var chest=st.AddPlacedObject(ItemType.Chest,25,1,BuildLayer.Solid);
    var hive=st.AddPlacedObject(ItemType.Beehive,28,1,BuildLayer.Solid); hive.StoredOutput=2;
    var bear=WorkerBear(s,9003,20); Assert(WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _));
    OnlyPriority(bear.Worker!,WorkerJob.HarvestHoney); Advance(s,20);
    Assert(hive.StoredOutput==0 && chest.Contents.Get(ItemType.Honey)==2 && bear.Worker!.Cargo.IsEmpty);
});
Test("two workers cannot receive the same tree",()=> {
    var tree=new NaturalFeatureSpawn(101,30,0,NaturalFeatureKind.Tree,0); var s=Session(Flat(features:new[]{tree})); var st=s.GetIslandState(0);
    var chest=st.AddPlacedObject(ItemType.Chest,25,1,BuildLayer.Solid); var a=WorkerBear(s,9004,20); var b=WorkerBear(s,9005,21);
    Assert(WorkerService.TryAssign(s,Query(s),a.ResidentId,chest.PlacementId,out _)); Assert(WorkerService.TryAssign(s,Query(s),b.ResidentId,chest.PlacementId,out _));
    OnlyPriority(a.Worker!,WorkerJob.HarvestTrees); OnlyPriority(b.Worker!,WorkerJob.HarvestTrees); Advance(s,40);
    int expected=HarvestRules.GetRewards(tree,new InventoryState()).Where(r=>r.Item==ItemType.Wood).Sum(r=>r.Amount);
    Assert(chest.Contents.Get(ItemType.Wood)==expected,"tree was paid more than once");
});
Test("player taking a reserved target prevents worker payout",()=> {
    var tree=new NaturalFeatureSpawn(102,30,0,NaturalFeatureKind.Tree,0); var s=Session(Flat(features:new[]{tree})); var st=s.GetIslandState(0);
    var chest=st.AddPlacedObject(ItemType.Chest,25,1,BuildLayer.Solid); var bear=WorkerBear(s,9006,20);
    Assert(WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _)); OnlyPriority(bear.Worker!,WorkerJob.HarvestTrees);
    Advance(s,0.5); Assert(bear.Worker!.TargetKind==WorkerTargetKind.NaturalFeature);
    Assert(WorldActions.TryHarvestNatural(Query(s),s.Inventory,WorldGrid.Feet(30,0),tree.FeatureId,out _)); Advance(s,20);
    Assert(chest.Contents.Get(ItemType.Wood)==0);
});
Test("missing home chest blocks worker but preserves cargo",()=> {
    var s=Session(Flat()); var st=s.GetIslandState(0); var chest=st.AddPlacedObject(ItemType.Chest,25,1,BuildLayer.Solid); var bear=WorkerBear(s,9007,20);
    Assert(WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _)); bear.Worker!.Cargo.Set(ItemType.Wood,3); st.RemovePlacedObject(chest.PlacementId);
    Advance(s,1); Assert(bear.Worker.Cargo.Get(ItemType.Wood)==3 && bear.Worker.Phase==WorkerPhase.Blocked && bear.Worker.BlockedReason.Contains("missing"));
});
Test("worker recall and save reload preserve cargo",()=> {
    var s=Session(Flat()); var chest=s.GetIslandState(0).AddPlacedObject(ItemType.Chest,25,1,BuildLayer.Solid); var bear=WorkerBear(s,9008,20);
    Assert(WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _)); bear.Worker!.Cargo.Set(ItemType.Honey,2);
    Assert(WorkerService.TryRecall(s,bear.ResidentId,out _)); var copy=Clone(s); var restored=copy.FindResident(bear.ResidentId)!;
    Assert(restored.Status==ResidentStatus.Roster && restored.Worker!.Cargo.Get(ItemType.Honey)==2);
});
Test("worker priority cycles independently",()=> {
    var s=Session(Flat()); var bear=WorkerBear(s,9009); bear.Worker=new WorkerState(); int before=bear.Worker.Priority(WorkerJob.GatherFlowers);
    Assert(WorkerService.TryCyclePriority(s,bear.ResidentId,WorkerJob.GatherFlowers,out _));
    Assert(bear.Worker.Priority(WorkerJob.GatherFlowers)==(before+1)%4 && bear.Worker.Priority(WorkerJob.HarvestHoney)==WorkerState.DefaultPriority(WorkerJob.HarvestHoney));
});
Test("protected hive garden is skipped by flower workers",()=> {
    var near=new NaturalFeatureSpawn(201,27,0,NaturalFeatureKind.Flower,0); var far=new NaturalFeatureSpawn(202,39,0,NaturalFeatureKind.Flower,1);
    var s=Session(Flat(features:new[]{near,far})); var st=s.GetIslandState(0); var chest=st.AddPlacedObject(ItemType.Chest,25,1,BuildLayer.Solid);
    st.AddPlacedObject(ItemType.Beehive,28,1,BuildLayer.Solid); var bear=WorkerBear(s,9010,20);
    Assert(WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _)); OnlyPriority(bear.Worker!,WorkerJob.GatherFlowers); bear.Worker!.ProtectHiveFlowers=true;
    Advance(s,40); Assert(!st.IsNaturalFeatureHarvested(near.FeatureId)); Assert(st.IsNaturalFeatureHarvested(far.FeatureId));
});
Test("worker plants sapling from home chest",()=> {
    var s=Session(Flat()); var st=s.GetIslandState(0); var chest=st.AddPlacedObject(ItemType.Chest,25,1,BuildLayer.Solid); chest.Contents.Set(ItemType.Sapling,1);
    var bear=WorkerBear(s,9011,20); Assert(WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _)); OnlyPriority(bear.Worker!,WorkerJob.PlantTrees);
    Advance(s,30); Assert(chest.Contents.Get(ItemType.Sapling)==0 && st.PlacedObjects.Any(p=>p.Item==ItemType.Sapling));
});
Test("worker gathers mushrooms grass flowers and cactus",()=> {
    var features=new[]{ new NaturalFeatureSpawn(301,27,0,NaturalFeatureKind.Mushroom,0), new NaturalFeatureSpawn(302,29,0,NaturalFeatureKind.Grass,0),
        new NaturalFeatureSpawn(303,31,0,NaturalFeatureKind.Flower,2), new NaturalFeatureSpawn(304,33,0,NaturalFeatureKind.Cactus,0)};
    var s=Session(Flat(features:features)); var st=s.GetIslandState(0); var chest=st.AddPlacedObject(ItemType.Chest,25,1,BuildLayer.Solid); var bear=WorkerBear(s,9012,20);
    Assert(WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _));
    foreach(var job in Enum.GetValues<WorkerJob>()) bear.Worker!.SetPriority(job,0);
    foreach(var job in new[]{WorkerJob.GatherMushrooms,WorkerJob.GatherGrass,WorkerJob.GatherFlowers,WorkerJob.HarvestCacti}) bear.Worker!.SetPriority(job,3);
    bear.Worker!.ProtectHiveFlowers=false; Advance(s,120);
    Assert(chest.Contents.Get(ItemType.MushroomBrown)>=1 && chest.Contents.Get(ItemType.Grass)>=1 && chest.Contents.Get(ItemType.BlueFlower)>=1 && chest.Contents.Get(ItemType.Cactus)>=1);
});
Test("worker can navigate mine ladder and mine reachable stone",()=> {
    var s=Session(Flat()); var st=s.GetIslandState(0); var chest=st.AddPlacedObject(ItemType.Chest,37,1,BuildLayer.Solid); var bear=WorkerBear(s,9013,37);
    Assert(WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _)); OnlyPriority(bear.Worker!,WorkerJob.MineStone);
    bear.Worker!.CurrentCellX=38; bear.Worker.CurrentLogicalLevel=-4; bear.Worker.WorkCenterCellX=38; bear.Worker.WorkCenterLogicalLevel=-5; bear.Worker.WorkRadiusCells=12;
    Advance(s,80); Assert(st.MinedUndergroundCells.Any(c=>c.LogicalLevel<0)); Assert(chest.Contents.Get(ItemType.Stone)>0);
});
Test("route invalidation prevents teleport through new wall",()=> {
    var tree=new NaturalFeatureSpawn(401,30,0,NaturalFeatureKind.Tree,0); var s=Session(Flat(features:new[]{tree})); var st=s.GetIslandState(0);
    var chest=st.AddPlacedObject(ItemType.Chest,20,1,BuildLayer.Solid); var bear=WorkerBear(s,9014,21);
    Assert(WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _)); OnlyPriority(bear.Worker!,WorkerJob.HarvestTrees); Advance(s,0.5);
    st.AddPlacedObject(ItemType.Stone,25,1,BuildLayer.Solid); st.AddPlacedObject(ItemType.Stone,25,2,BuildLayer.Solid); Advance(s,15);
    Assert(!st.IsNaturalFeatureHarvested(tree.FeatureId));
});
Test("worker state round trips in schema 4",()=> {
    var s=Session(Flat()); var chest=s.GetIslandState(0).AddPlacedObject(ItemType.Chest,25,1,BuildLayer.Solid); var bear=WorkerBear(s,9015,20);
    Assert(WorkerService.TryAssign(s,Query(s),bear.ResidentId,chest.PlacementId,out _)); bear.Worker!.Cargo.Set(ItemType.Wood,7); bear.Worker.SetPriority(WorkerJob.GatherFlowers,3);
    var copy=Clone(s); var worker=copy.FindResident(bear.ResidentId)!.Worker!;
    Assert(worker.HomeChestPlacementId==chest.PlacementId && worker.Cargo.Get(ItemType.Wood)==7 && worker.Priority(WorkerJob.GatherFlowers)==3);
});


GeneratedStructureDefinition House(int center=20,int id=0) =>
    new(id,GeneratedStructureKind.House,center,6,0,
        new GeneratedChestDefinition(1000+id,center,1,new Dictionary<ItemType,int>{{ItemType.Wood,4}}));
GeneratedStructureDefinition Castle(int center=30,int id=0) =>
    new(id,GeneratedStructureKind.Castle,center,10,0,
        new GeneratedChestDefinition(2000+id,center,1,new Dictionary<ItemType,int>{{ItemType.Stone,8}}));

Test("generated house has a traversable doorway and reachable chest room",()=> {
    var d=Flat(structures:new[]{House()}); var s=Session(d); var q=Query(s);
    var layout=q.StructureLayouts.Single();
    Assert(!q.Solid(new(layout.DoorCellX,1)) && !q.Solid(new(layout.DoorCellX,2)),"door cells must stay open");
    Assert(!q.Solid(new(d.GeneratedStructures[0].Chest.CellX,d.GeneratedStructures[0].Chest.LogicalLevel)),"generated chest must not be embedded in a wall");
    var outside=new UndergroundCell(layout.DoorCellX<d.WidthCells/2?layout.DoorCellX-1:layout.DoorCellX+1,1);
    var inside=new UndergroundCell(layout.DoorCellX<d.WidthCells/2?layout.DoorCellX+1:layout.DoorCellX-1,1);
    Assert(WorkerNavigation.TryPathLength(q,outside,inside,out _),"doorway should connect outside to interior");
});
Test("player furniture can be placed inside a generated house",()=> {
    var d=Flat(structures:new[]{House()}); var s=Session(d); var q=Query(s); var layout=q.StructureLayouts.Single();
    int x=layout.LeftCellX+1; if(x==d.GeneratedStructures[0].Chest.CellX) x++;
    Assert(q.CanPlace(ItemType.Chest,new(x,1),BuildLayer.Solid,WorldGrid.BearRect(WorldGrid.Center(layout.RightCellX+2,1)),out var why),why);
});
Test("generated structure cells and their support cannot be mined",()=> {
    var d=Flat(structures:new[]{House()}); var s=Session(d); var q=Query(s); var layout=q.StructureLayouts.Single();
    Assert(!q.CanMine(new(layout.LeftCellX,0),out _));
    Assert(!q.CanMine(new(layout.LeftCellX,-1),out _));
});
Test("generated structures reserve vegetation and regrowth space",()=> {
    var structure=House(); var feature=new NaturalFeatureSpawn(777,structure.CenterCellX,0,NaturalFeatureKind.Tree,0);
    var s=Session(Flat(features:new[]{feature},structures:new[]{structure}));
    Assert(!Query(s).NaturalPresent(feature));
});
Test("castle upper room is reachable by generated ladder",()=> {
    var s=Session(Flat(structures:new[]{Castle()})); var q=Query(s); var layout=q.StructureLayouts.Single();
    int ladderX=layout.LadderCells.First().CellX;
    var lower=new UndergroundCell(ladderX,1);
    var upper=new UndergroundCell(ladderX,4);
    Assert(WorkerNavigation.IsStandable(q,lower));
    Assert(WorkerNavigation.IsStandable(q,upper));
    Assert(WorkerNavigation.TryPathLength(q,lower,upper,out int steps) && steps>0);
});
Test("structure layout is deterministic from materialized definition",()=> {
    var d=Flat(structures:new[]{Castle()});
    var a=GeneratedStructureLayoutService.BuildAll(d);
    var b=GeneratedStructureLayoutService.BuildAll(d);
    Assert(a[0].SolidCells.SetEquals(b[0].SolidCells) && a[0].LadderCells.SetEquals(b[0].LadderCells));
});
Test("map lists settlement production and worker status",()=> {
    var s=Session(Flat()); var state=s.GetIslandState(0);
    state.SetBoatBuilt(BoatSide.Right,true);
    var hive=state.AddPlacedObject(ItemType.Beehive,20,1,BuildLayer.Solid); hive.StoredOutput=3;
    state.AddPlacedObject(ItemType.Chest,24,1,BuildLayer.Solid);
    var bear=WorkerBear(s,9901,24); bear.Worker=new WorkerState {HomeChestPlacementId=1,Phase=WorkerPhase.Blocked,BlockedReason="test"};
    var entry=DiscoveryMapService.Build(s,new IslandGenerator(),BoatSide.Right).Single();
    Assert(entry.WorkerCount==1 && entry.BlockedWorkerCount==1 && entry.PlayerChestCount==1 && entry.ReadyHoney==3);
});
Test("map direct travel requires discovered destination",()=> {
    var s=Session(Flat()); s.GetIslandState(0).SetBoatBuilt(BoatSide.Right,true);
    Throws<InvalidOperationException>(()=>TravelService.PrepareDirect(s,BoatSide.Right,2,BoatSide.Left,id=>Flat(id)));
});
Test("map direct travel requires destination dock",()=> {
    var s=Session(Flat()); s.GetIslandState(0).SetBoatBuilt(BoatSide.Right,true);
    var target=Flat(id:2); s.SetBaseline(target); s.SetIslandState(2,new IslandDeltaState()); s.DiscoverIsland(2);
    Throws<InvalidOperationException>(()=>TravelService.PrepareDirect(s,BoatSide.Right,2,BoatSide.Left,id=>target));
});
Test("map direct travel uses an existing destination dock without new discovery",()=> {
    var s=Session(Flat()); s.GetIslandState(0).SetBoatBuilt(BoatSide.Right,true);
    var target=Flat(id:2,biome:BiomeType.Snowy); var targetState=new IslandDeltaState(); targetState.SetBoatBuilt(BoatSide.Left,true);
    s.SetBaseline(target); s.SetIslandState(2,targetState); s.DiscoverIsland(2);
    int discovered=s.DiscoveredIslandIds.Count;
    var prepared=TravelService.PrepareDirect(s,BoatSide.Right,2,BoatSide.Left,id=>target);
    TravelService.Commit(s,prepared);
    Assert(s.CurrentIslandId==2 && s.DiscoveredIslandIds.Count==discovered && s.PlayerLocation?.IslandId==2);
});
Test("map service prefers dock facing travel direction then falls back",()=> {
    var target=new IslandDeltaState(); target.SetBoatBuilt(BoatSide.Right,true);
    Assert(DiscoveryMapService.ChooseArrival(0,3,target)==BoatSide.Right);
    target.SetBoatBuilt(BoatSide.Left,true);
    Assert(DiscoveryMapService.ChooseArrival(0,3,target)==BoatSide.Left);
});
Test("undiscovered islands never appear in map entries",()=> {
    var s=Session(Flat()); s.SetBaseline(Flat(id:4,biome:BiomeType.Mountain));
    var entries=DiscoveryMapService.Build(s,new IslandGenerator(),null);
    Assert(entries.Count==1 && entries[0].IslandId==0);
});

string temp=Path.Combine(Path.GetTempPath(),"BearAdventureChecks-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
try
{
    Test("save and reload round trip",()=> {
        var store=new SaveGameStore(); var s=Session(Flat()); s.Inventory.Set(ItemType.Wood,5); string path=Path.Combine(temp,"normal.json");
        store.Save(path,GameSaveData.FromSession(s)); var load=store.LoadSession(path);
        Assert(load.Status==SaveLoadStatus.Loaded && load.Session!.Inventory.Get(ItemType.Wood)==5);
    });
    Test("corrupt primary recovers without poisoning backup",()=> {
        var store=new SaveGameStore(); var s=Session(Flat()); s.Inventory.Set(ItemType.Wood,5); string path=Path.Combine(temp,"recover.json");
        store.Save(path,GameSaveData.FromSession(s)); s.Inventory.Set(ItemType.Wood,8); store.Save(path,GameSaveData.FromSession(s));
        string backup=File.ReadAllText(path+".bak"); File.WriteAllText(path,"{broken");
        var load=store.LoadSession(path); Assert(load.Status==SaveLoadStatus.RecoveredBackup && load.Session!.Inventory.Get(ItemType.Wood)==5);
        store.Save(path,GameSaveData.FromSession(load.Session!)); Assert(File.ReadAllText(path+".bak")==backup);
        Assert(Directory.GetFiles(temp,"recover.json.rejected-*").Length==1);
    });
    Test("invalid primary plus invalid backup is a protected failure",()=> {
        string path=Path.Combine(temp,"bad.json"); File.WriteAllText(path,"bad-primary"); File.WriteAllText(path+".bak","bad-backup"); var store=new SaveGameStore();
        Assert(store.LoadSession(path).Status==SaveLoadStatus.Failed);
        Throws<InvalidDataException>(()=>store.Save(path,GameSaveData.FromSession(Session(Flat()))));
        Assert(File.ReadAllText(path)=="bad-primary" && File.ReadAllText(path+".bak")=="bad-backup");
    });
    Test("failed write before replacement retains primary and backup",()=> {
        string path=Path.Combine(temp,"fault.json"); var store=new SaveGameStore(); var s=Session(Flat());
        store.Save(path,GameSaveData.FromSession(s)); s.Inventory.Set(ItemType.Wood,3); store.Save(path,GameSaveData.FromSession(s));
        string before=File.ReadAllText(path), backup=File.ReadAllText(path+".bak");
        store.FaultInjector=stage=>{if(stage==SaveWriteStage.BeforeCommit) throw new IOException("Injected interrupted write");};
        s.Inventory.Set(ItemType.Wood,8); Throws<IOException>(()=>store.Save(path,GameSaveData.FromSession(s)));
        Assert(File.ReadAllText(path)==before && File.ReadAllText(path+".bak")==backup);
        Assert(Directory.GetFiles(temp,"fault.json.tmp-*").Length==0);
    });
    Test("write failure after flush retains last good save",()=> {
        string path=Path.Combine(temp,"flush-fault.json"); var store=new SaveGameStore(); var s=Session(Flat()); store.Save(path,GameSaveData.FromSession(s));
        var before=File.ReadAllText(path); store.FaultInjector=stage=>{if(stage==SaveWriteStage.AfterFlush) throw new IOException("Injected");};
        Throws<IOException>(()=>store.Save(path,GameSaveData.FromSession(s))); Assert(File.ReadAllText(path)==before);
    });
    Test("different world cannot overwrite existing path",()=> {
        string path=Path.Combine(temp,"world-guard.json"); var store=new SaveGameStore(); store.Save(path,GameSaveData.FromSession(Session(Flat())));
        var other=new GameSessionState(new InventoryState(),"another-world"); other.SetBaseline(Flat()); other.GetIslandState(0);
        Throws<InvalidDataException>(()=>store.Save(path,GameSaveData.FromSession(other)));
    });
    Test("legacy original bytes preserved at migration commit",()=> {
        string path=Path.Combine(temp,"legacy.json"); string original=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","legacy-v1.json"));
        File.WriteAllText(path,original); var store=new SaveGameStore(); var load=store.LoadSession(path);
        store.Save(path,GameSaveData.FromSession(load.Session!)); Assert(File.ReadAllText(path+".pre-v2")==original && File.ReadAllText(path+".pre-v3")==original && File.ReadAllText(path+".pre-v4")==original);
        Assert(store.LoadSession(path).SourceFormat==4);
    });
    Test("legacy backup migration preserves pre-v2 source",()=> {
        string path=Path.Combine(temp,"legacy-recovery.json"); string original=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","legacy-v1.json"));
        File.WriteAllText(path,"{broken"); File.WriteAllText(path+".bak",original); var store=new SaveGameStore(); var load=store.LoadSession(path);
        Assert(load.Status==SaveLoadStatus.RecoveredBackup);
        store.Save(path,GameSaveData.FromSession(load.Session!)); Assert(File.ReadAllText(path+".pre-v2")==original && File.ReadAllText(path+".bak")==original);
        store.Save(path,GameSaveData.FromSession(load.Session!)); Assert(File.ReadAllText(path+".pre-v2")==original);
    });
    Test("no save exists is distinct from failed load",()=> {
        var store=new SaveGameStore(); Assert(store.LoadSession(Path.Combine(temp,"does-not-exist.json")).Status==SaveLoadStatus.NewWorld);
    });
}
finally { Directory.Delete(temp,recursive:true); }
Console.WriteLine($"GAMEPLAY CHECKS: {passed} passed; {failed} failed.");
System.Environment.ExitCode=failed==0?0:1;
