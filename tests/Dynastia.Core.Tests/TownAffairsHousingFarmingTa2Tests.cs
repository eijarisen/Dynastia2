using System.Text.Json;
using Dynastia.Contracts;
using Dynastia.Mechanics.Economy;

namespace Dynastia.Core.Tests;

public sealed class TownAffairsHousingFarmingTa2Tests
{
    [Fact]
    public void TownPickerOrdersResidenceThenOwnedTownsThenAlphabeticallyAndShowsNoPrices()
    {
        var code = Read("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.Actions.cs");
        var window = Read("src", "Dynastia.App", "Views", "MainWindow.axaml.cs");

        Assert.Contains("currentTown.Id", code);
        Assert.Contains("ownedTownIds.Contains", code);
        Assert.Contains(".OrderBy(item => item.Category)", code);
        Assert.Contains(".ThenBy(item => item.Option.PrimaryText", code);
        Assert.Contains("View Housing", window);

        var buyStart = code.IndexOf(
            "actionId.Equals(\"household.buy_house\"",
            StringComparison.Ordinal);
        var nextBranch = code.IndexOf(
            "actionId.Equals(\"household.sell_house\"",
            buyStart,
            StringComparison.Ordinal);
        Assert.True(buyStart >= 0 && nextBranch > buyStart);
        var buyPicker = code[buyStart..nextBranch];
        Assert.DoesNotContain("GetHousePrice", buyPicker);
        Assert.DoesNotContain("summaryPrice", buyPicker);
    }

    [Fact]
    public void PropertySelectorRetainsTownSearch()
    {
        var xaml = Read("src", "Dynastia.App", "Views", "PropertySelectionWindow.axaml");
        var code = Read("src", "Dynastia.App", "Views", "PropertySelectionWindow.axaml.cs");

        Assert.Contains("Search", xaml);
        Assert.Contains("TextChanged", xaml);
        Assert.Contains("Search", code);
    }

    [Fact]
    public void MarketRulesMatchBatchTwoCapacityCountAndPricingTargets()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryRoot(), "data", "Housing", "house_market_rules.json")));
        var root = document.RootElement;

        Assert.Equal(6, root.GetProperty("standardCapacity").GetInt32());
        Assert.Equal(new[] { 2, 4, 6, 8 },
            root.GetProperty("offerCapacities").EnumerateArray().Select(value => value.GetInt32()).ToArray());

        var multipliers = root.GetProperty("capacityPriceMultipliers");
        Assert.Equal(0.70m, multipliers.GetProperty("2").GetDecimal());
        Assert.Equal(0.85m, multipliers.GetProperty("4").GetDecimal());
        Assert.Equal(1.00m, multipliers.GetProperty("6").GetDecimal());
        Assert.Equal(1.15m, multipliers.GetProperty("8").GetDecimal());

        var counts = root.GetProperty("offerCountRangeBySettlement");
        Assert.Equal(new[] { 0, 2 }, ReadPair(counts.GetProperty("SmallTown")));
        Assert.Equal(new[] { 0, 3 }, ReadPair(counts.GetProperty("Town")));
        Assert.Equal(new[] { 1, 4 }, ReadPair(counts.GetProperty("City")));
        Assert.Equal(new[] { 2, 5 }, ReadPair(counts.GetProperty("MajorCity")));

        var offerRange = root.GetProperty("offerRandomPriceMultiplier");
        Assert.Equal(0.80m, offerRange[0].GetDecimal());
        Assert.Equal(1.20m, offerRange[1].GetDecimal());
        Assert.Equal(0.90m, root.GetProperty("prosperityPrice").GetProperty("minimum").GetDecimal());
        Assert.Equal(1.10m, root.GetProperty("prosperityPrice").GetProperty("maximum").GetDecimal());
    }

    [Fact]
    public void HouseCapacityDefaultsToSixAndExtensionsRemainPlusTwoAtQuarterPurchasePrice()
    {
        var town = new TownInfo("Test", "Test", 0, 0, 10_000) { Id = "test" };
        var standard = new HousePropertyInfo(Guid.NewGuid(), town, true, false, PurchasePrice: 40_000m);
        var small = standard with { BaseResidentCapacity = 2 };
        var largeExtended = standard with { BaseResidentCapacity = 8, CapacityExtensions = 2 };

        Assert.Equal(6, standard.ResidentCapacity);
        Assert.Equal(2, small.ResidentCapacity);
        Assert.Equal(12, largeExtended.ResidentCapacity);
        Assert.Equal(10_000m, standard.ExtensionCost);
        Assert.Equal(6, HouseExtensionRules.GetResidentCapacity(0));
        Assert.Equal(8, HouseExtensionRules.GetResidentCapacity(1));
    }

    [Fact]
    public void HouseOffersAreDeterministicWithoutUsingGlobalGameRandom()
    {
        var code = Read("plugins", "Dynastia.Mechanics.Economy", "StandardHouseMarketService.cs");

        Assert.Contains("SHA256.HashData", code);
        Assert.Contains("BuildKey(town.Id, year", code);
        Assert.DoesNotContain("IGameRandom", code);
        Assert.DoesNotContain("Random.Shared", code);
        Assert.Contains("_locations.FindTownAtYear(townId, offerYear)", code);
    }

    [Fact]
    public void QueuedPurchaseStoresAndStrictlyRevalidatesExactOfferSnapshot()
    {
        var app = Read("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var action = Read("plugins", "Dynastia.Mechanics.Households", "HouseholdsPlugin.PropertyActions.cs");

        foreach (var field in new[]
                 {
                     "houseOfferId", "houseOfferYear", "houseCapacity", "houseAskingPrice",
                     "summaryTown", "summaryPrice", "summaryCapacity"
                 })
        {
            Assert.Contains(field, app);
        }

        foreach (var field in new[]
                 {
                     "houseOfferId", "houseOfferYear", "houseCapacity", "houseAskingPrice"
                 })
        {
            Assert.Contains(field, action);
        }

        Assert.Contains("ResolveOffer", action);
        Assert.Contains("offer.BaseResidentCapacity != capacity", action);
        Assert.Contains("offer.AskingPrice != askingPrice", action);
        Assert.Contains("economy.AddHouse(", action);
        Assert.Contains("baseCapacity);", action);
    }

    [Fact]
    public void PropertyEconomicsUseCapacityProsperityAndSpecificHouseForRent()
    {
        var assets = Read("plugins", "Dynastia.Mechanics.Economy", "StandardEconomyService.Assets.cs");
        var finance = Read("plugins", "Dynastia.Mechanics.Economy", "StandardEconomyService.AnnualFinance.cs");

        Assert.Contains("GetCapacityMultiplier", assets);
        Assert.Contains("GetProsperityMultiplier", assets);
        Assert.Contains("HouseExtensionRules.ExtensionPriceFraction", assets);
        Assert.Contains("house.CapacityExtensions", assets);
        Assert.Contains("GetHouseValue(house)", assets);
        Assert.Contains("SaleValueMultiplier", assets);
        Assert.Contains(".Sum(GetRentalIncome)", finance);
    }

    [Fact]
    public void HousingTabSupportsRemotePurchaseWithoutRelocatingAndShowsEmptyMarketMessage()
    {
        var hub = Read("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");
        var owner = Read("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var window = Read("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var action = Read("plugins", "Dynastia.Mechanics.Households", "HouseholdsPlugin.PropertyActions.cs");

        Assert.Contains("RemoteHousingBrowse", hub);
        Assert.Contains("No houses are currently offered for sale in this town.", hub);
        Assert.Contains("QueueHousePurchase", hub);
        Assert.DoesNotContain("BaseHouseMarketValue", hub);
        Assert.Contains("HouseOffers", hub);
        Assert.DoesNotContain("OwnedHouses", hub);
        Assert.Contains("OnBuyHouseOfferClick", window);
        Assert.DoesNotContain("SetHouseholdHomeTown", action);
        Assert.DoesNotContain("SetPersonHomeTown", action);
        Assert.Contains("QueueTownAffairsHousePurchase", owner);
    }

    private static int[] ReadPair(JsonElement element) =>
        element.EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dynastia.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
