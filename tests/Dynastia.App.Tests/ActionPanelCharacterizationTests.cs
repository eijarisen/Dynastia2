using Dynastia.App.ViewModels;
using Dynastia.Contracts;

namespace Dynastia.App.Tests;

public sealed class ActionPanelCharacterizationTests
{
    [Fact]
    public void PanelGroupsPropertyFinanceAndCraftActionsWithoutLosingPassOrLocalServices()
    {
        using var f = new ActionPanelFixture();
        f.Register("household.buy_house", "household.extend_house", "farming.buy_farmland", "heirloom.sell",
            "loan.take", "loan.give", "economy.lifestyle.balanced", "economy.lifestyle.lavish", "economy.lifestyle.thrifty",
            "craft.start.carpentry", "craft.start.writing", "church.attend", "personality.religious_study",
            "wellbeing.therapy", "wellbeing.heal_relative", "turn.pass");
        f.Select();
        var ids = f.View.AvailableActions.Select(action => action.Id).ToArray();
        Assert.Equal(1, ids.Count(id => id == "ui.manage_properties"));
        Assert.Equal(1, ids.Count(id => id == "ui.manage_finances"));
        Assert.Equal(1, ids.Count(id => id == "ui.craft_profession"));
        Assert.Contains("ui.town_affairs", ids);
        Assert.Contains("wellbeing.therapy", ids);
        Assert.Contains("wellbeing.heal_relative", ids);
        Assert.DoesNotContain("church.attend", ids);
        Assert.DoesNotContain("personality.religious_study", ids);
        Assert.DoesNotContain("ui.self_improvement", ids);
        Assert.DoesNotContain(ids, id => id.StartsWith("economy.lifestyle.", StringComparison.Ordinal));
        Assert.DoesNotContain(ids, id => id.StartsWith("craft.start.", StringComparison.Ordinal));
        Assert.Equal("turn.pass", ids[^1]);
        Assert.Equal(1, ids.Count(id => id == "turn.pass"));
        Assert.Equal("turn.pass", Assert.Single(f.View.PassActions).Id);
    }

    [Theory]
    [InlineData("career.seek_employment", TownAffairsTab.Jobs)]
    [InlineData("career.find_another_job", TownAffairsTab.Jobs)]
    [InlineData("career.help_seek_employment", TownAffairsTab.Jobs)]
    [InlineData("career.help_find_better_job", TownAffairsTab.Jobs)]
    [InlineData("education.get_education", TownAffairsTab.Education)]
    [InlineData("wellbeing.heal_relative", TownAffairsTab.Health)]
    [InlineData("wellbeing.therapy", TownAffairsTab.Health)]
    public void ServiceActionOpensSelectionAndKeepsItsTownRouteAndSubject(string id, TownAffairsTab tab)
    {
        using var f = new ActionPanelFixture();
        f.Register(id);
        f.Select();
        var requests = new List<string>();
        f.View.ActionSelectionRequested += (_, e) => requests.Add(e.ActionId);
        Assert.Single(f.View.AvailableActions.Where(a => a.Id == id)).ExecuteCommand.Execute(null);
        Assert.Equal(id, Assert.Single(requests));
        Assert.Empty(f.Actions.GetQueuedActions(f.Head));
        var route = f.View.CreateTownAffairsRequest(id);
        Assert.NotNull(route);
        Assert.Equal(tab, route.InitialTab);
        Assert.Equal(f.Head.Id, route.SubjectPersonId);
    }

    [Fact]
    public void DirectTownAccessUsesCurrentMembershipAndChildTreatmentRetainsItsShortcut()
    {
        using var f = new ActionPanelFixture();
        var resident = f.Person(30, "Resident");
        f.Economy.Members.Add(resident.Id);
        f.Select(resident);
        Assert.False(f.Succession.IsControllable(resident));
        Assert.True(f.View.CanOpenTownAffairs);
        f.Economy.Members.Remove(resident.Id);
        f.Select(resident);
        Assert.False(f.View.CanOpenTownAffairs);
        resident.Tags.Remove("state.alive");
        f.Economy.Members.Add(resident.Id);
        f.Select(resident);
        Assert.False(f.View.CanOpenTownAffairs);
        resident.Tags.Add("state.alive");
        resident.Age = 10;
        f.Economy.Members.Add(resident.Id);
        f.Select(resident);
        Assert.False(f.View.CanOpenTownAffairs);
        Assert.Null(f.View.CreateTownAffairsRequest("ui.town_affairs"));
        Assert.Equal(TownAffairsTab.Health, f.View.CreateTownAffairsRequest("wellbeing.heal_relative")!.InitialTab);
    }

    [Fact]
    public void TownPickerOrdersResidenceThenOwnedThenOtherTownsWithCompactSearchableDetailsAndNoPrices()
    {
        using var f = new ActionPanelFixture();
        var residence = f.Economy.Residence;
        var alpha = residence with { Id = "alpha", Town = "A Other" };
        var beta = residence with { Id = "beta", Town = "B Other" };
        var owned = residence with { Id = "owned", Town = "Y Owned" };
        f.Locations.Towns.AddRange([beta, owned, alpha]);
        f.Economy.Houses.Add(new HousePropertyInfo(Guid.Parse("10000000-0000-0000-0000-000000000001"), owned, false, true));
        var options = f.View.GetPropertySelectionOptions("household.buy_house");
        Assert.Equal(new[] { residence.Id, owned.Id, alpha.Id, beta.Id }, options.Select(option => option.Id));
        Assert.Equal(0, f.Economy.PriceReads);
        Assert.All(options, option =>
        {
            Assert.Equal("Polity • Region • County", option.SecondaryText);
            Assert.Contains("Population:", option.DetailsText);
            Assert.Contains("Prosperity: 100 (Stable)", option.DetailsText);
            Assert.Contains("Opportunities: Arts, Trade, Heavy Industry", option.DetailsText);
            Assert.DoesNotContain('\n', option.DetailsText);
            Assert.DoesNotContain("zł", option.DetailsText);
            Assert.Equal(string.Empty, option.PriceText);
            Assert.Contains("Region", option.SearchText);
            Assert.Contains("Heavy Industry", option.SearchText);
        });
    }

    [Fact]
    public void FarmlandSelectorReadsTheActiveHouseholdAndRetainsParcelIdentityAndSalvageValue()
    {
        var farming = new FarmingStub();
        using var f = new ActionPanelFixture(farming);
        var parcel = new FarmlandAssetInfo(Guid.Parse("10000000-0000-0000-0000-000000000001"),
            f.Economy.Residence, 1890, "purchase", FarmTypeId: "orchard", FarmTypeDisplayName: "Orchard",
            FarmTypeEmoji: "🍎", LivestockTypeId: "cattle", LivestockDisplayName: "Cattle", LivestockEmoji: "🐄");
        farming.Parcels.Add(parcel);
        var resident = f.Person(22, "Resident");
        f.Economy.Members.Add(resident.Id);
        f.Select(resident);
        farming.SnapshotRequests.Clear();
        farming.SaleRequests.Clear();
        var option = Assert.Single(f.View.GetPropertySelectionOptions("farming.sell_farmland"));
        Assert.Equal(f.Head.Id, Assert.Single(farming.SnapshotRequests));
        Assert.Equal(parcel.Id.ToString(), option.Id);
        Assert.Equal("🍎 Orchard", option.PrimaryText);
        Assert.Equal("Z Residence • County", option.SecondaryText);
        Assert.Contains("Livestock: 🐄 Cattle", option.DetailsText);
        Assert.Contains("Acquired: 1890", option.DetailsText);
        Assert.Equal(parcel.Id, Assert.Single(farming.SaleRequests));
        // This check tolerates locale-specific grouping while preserving the exact amount.
        Assert.Equal($"Sale: {12345m:N0} zł", option.PriceText);
    }

    [Theory]
    [InlineData("household.buy_house", "townId")]
    [InlineData("household.sell_house", "propertyId")]
    [InlineData("household.extend_house", "propertyId")]
    [InlineData("farming.sell_farmland", "farmlandId")]
    [InlineData("farming.add_livestock", "farmlandId")]
    [InlineData("heirloom.sell", "heirloomId")]
    [InlineData("household.ask_move_out", "propertyId")]
    public void SelectionQueuesExactAssetKeyAndPreservesMoveOutTarget(string id, string key)
    {
        using var f = new ActionPanelFixture();
        f.Register(id);
        var resident = f.Person(22, "Resident");
        f.Economy.Members.Add(resident.Id);
        f.Select(resident);
        const string selected = "20000000-0000-0000-0000-000000000002";
        f.View.QueueActionWithSelection(id, selected);
        var queued = Assert.Single(f.Actions.GetQueuedActions(f.Head));
        Assert.Equal(selected, queued.Parameters![key]);
        Assert.Equal(f.Head.Id, queued.ActorId);
        Assert.Equal(id == "household.ask_move_out" ? resident.Id : f.Head.Id, queued.TargetId);
        Assert.True(f.View.HasQueuedAction);
        Assert.Empty(f.View.AvailableActions);
        Assert.Empty(f.View.PassActions);
    }

    [Theory]
    [InlineData("farming.buy_farmland", "Queued: 🌾 Buy Farmland – 12,345 zł")]
    [InlineData("farming.sell_farmland", "Queued: 🌾 Sell Farmland – Farm — 12,345 zł")]
    [InlineData("farming.add_livestock", "Queued: 🌾 Add Livestock – Farm — 12,345 zł")]
    public void QueuedFarmlandLabelsAndDetailsAreCleanAndDoNotAppendPersonNames(string id, string expected)
    {
        using var f = new ActionPanelFixture();
        f.Register(id);
        Assert.True(f.Actions.Execute(id, f.Head, f.Head,
            new Dictionary<string, string> { ["summaryFarmland"] = "Farm", ["summaryPrice"] = "12345" }).Success);
        f.Select();
        Assert.Equal(expected, f.View.QueuedActionText);
    }

    private sealed class FarmingStub : IFarmingService
    {
        public List<FarmlandAssetInfo> Parcels { get; } = [];
        public List<Guid> SnapshotRequests { get; } = [];
        public List<Guid> SaleRequests { get; } = [];
        public decimal PurchasePrice => 20000m;
        public decimal SalePrice => 10000m;
        public decimal LivestockPurchasePrice => 5000m;
        public decimal LivestockSalePrice => 2345m;
        public FarmingHouseholdSnapshot GetSnapshot(IPerson representative)
        {
            SnapshotRequests.Add(representative.Id);
            return new(Parcels, "residence", Parcels.Count, 0, 0, 0m, 0m);
        }
        public bool IsAvailableFarmWorker(IPerson person, IPerson representative) => false;
        public bool IsWorkingFarmWorker(IPerson person, IPerson representative) => false;
        public decimal GetExpectedAnnualIncome(IPerson representative) => 0m;
        public decimal GetExpectedAnnualIncomeAfterAddingLocalParcel(IPerson representative) => 0m;
        public IReadOnlyList<FarmingFlavorInfo> GetAvailableLivestockOptions(TownInfo town, int year) => [];
        public decimal GetFarmlandSaleValue(FarmlandAssetInfo farmland)
        { SaleRequests.Add(farmland.Id); return SalePrice + LivestockSalePrice; }
        public FarmlandAssetInfo? AssignNewFarmlandType(IPerson representative, Guid id) => throw new NotSupportedException();
        public FarmlandAssetInfo? EnsureFarmlandFlavor(IPerson representative, Guid id) => throw new NotSupportedException();
        public FarmlandAssetInfo? AddLivestock(IPerson representative, Guid id, int year) => throw new NotSupportedException();
        public FarmlandRelocationSaleResult SellOriginFarmlandForVoluntaryRelocation(
            IPerson representative, TownInfo origin, TownInfo destination) => throw new NotSupportedException();
    }

}
