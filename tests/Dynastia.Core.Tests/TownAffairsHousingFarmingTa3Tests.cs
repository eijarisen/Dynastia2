using System.Globalization;
using System.Text.Json;
using Dynastia.Mechanics.Farming;

namespace Dynastia.Core.Tests;

public sealed class TownAffairsHousingFarmingTa3Tests
{
    [Fact]
    public void FlavorDataContainsAllAgreedFarmAndLivestockTypesAndRules()
    {
        var farms = ReadCsv("data", "Farming", "farm_types.csv");
        var livestock = ReadCsv("data", "Farming", "livestock_types.csv");

        Assert.Equal(
            new[] { "wheat", "rye", "vegetables", "orchard", "potatoes", "flax", "hops", "vineyard" },
            farms.Skip(1).Select(row => row[0]).ToArray());
        Assert.Equal(
            new[] { "chickens", "cows", "pigs", "sheep", "goats", "geese", "horses" },
            livestock.Skip(1).Select(row => row[0]).ToArray());

        using var rules = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryRoot(), "data", "Farming", "livestock_rules.json")));
        var root = rules.RootElement;
        Assert.Equal(2500m, root.GetProperty("purchasePrice").GetDecimal());
        Assert.Equal(2000m, root.GetProperty("saleValue").GetDecimal());
        Assert.True(root.GetProperty("oneLivestockUpgradePerFarmlandParcel").GetBoolean());
        Assert.Equal(0.1m,
            root.GetProperty("income").GetProperty("maximumBoostAtFullLocalCoverage").GetDecimal());
        Assert.Equal(0.2m,
            root.GetProperty("volatility").GetProperty("maximumCompressionAtFullLocalCoverage").GetDecimal());
    }

    [Fact]
    public void FlavorSelectionUsesYearRegionWeightsAndDeterministicLegacyBackfill()
    {
        var catalog = Read("plugins", "Dynastia.Mechanics.Farming", "FarmingFlavorCatalog.cs");
        var service = Read("plugins", "Dynastia.Mechanics.Farming", "StandardFarmingService.cs");

        Assert.Contains("definition.BaseWeight * time * region", catalog);
        Assert.Contains("year < definition.StartYear || year > definition.EndYear", catalog);
        Assert.Contains("regionWeights.TryGetValue", catalog);

        var ensureStart = service.IndexOf("public FarmlandAssetInfo? EnsureFarmlandFlavor", StringComparison.Ordinal);
        var ensureEnd = service.IndexOf("public FarmlandAssetInfo? AddLivestock", ensureStart, StringComparison.Ordinal);
        Assert.True(ensureStart >= 0 && ensureEnd > ensureStart);
        var ensure = service[ensureStart..ensureEnd];
        Assert.Contains("DeterministicRoll", ensure);
        Assert.DoesNotContain("_random.", ensure);
        Assert.Contains("SHA256.HashData", service);
        Assert.Contains("parcel.Id", ensure);
        Assert.Contains("parcel.Town.RegionId", ensure);
        Assert.Contains("parcel.AcquiredYear", ensure);
    }

    [Fact]
    public void FarmlandStatePersistsFlavorThroughExistingAndPendingInheritancePaths()
    {
        var state = Read("plugins", "Dynastia.Mechanics.Economy", "FarmlandAssetState.cs");
        var economy = Read("plugins", "Dynastia.Mechanics.Economy", "StandardEconomyService.Farmland.cs");

        Assert.Contains("FarmTypeId", state);
        Assert.Contains("LivestockTypeId", state);
        Assert.True(Count(economy, "FarmTypeId = farmland.FarmTypeId") >= 2);
        Assert.True(Count(economy, "LivestockTypeId = farmland.LivestockTypeId") >= 2);
        Assert.Contains("SetFarmlandFlavor", economy);
    }

    [Fact]
    public void FarmTypeIsFlavorOnlyWhileLivestockChangesIncomeAndVolatilityUniversally()
    {
        var service = Read("plugins", "Dynastia.Mechanics.Farming", "StandardFarmingService.cs");
        var expectedStart = service.IndexOf("private decimal GetExpectedAnnualIncomeForWorkers", StringComparison.Ordinal);
        var incomeStart = service.IndexOf("decimal IHouseholdIncomeProvider.GetAnnualIncome", expectedStart, StringComparison.Ordinal);
        Assert.True(expectedStart >= 0 && incomeStart > expectedStart);
        var expected = service[expectedStart..incomeStart];

        Assert.DoesNotContain("FarmTypeId", expected);
        Assert.Contains("GetLivestockIncomeMultiplier", expected);
        Assert.Contains("AdjustVolatilityMultiplier", service);
        Assert.Contains("GetLivestockCoverage", service);
    }

    [Fact]
    public void AddLivestockIsQueuedLocalOnePerParcelAndCostsTwentyFiveHundred()
    {
        var plugin = Read("plugins", "Dynastia.Mechanics.Farming", "FarmingPlugin.cs");

        Assert.Contains("Id = \"farming.add_livestock\"", plugin);
        Assert.Contains("QueuePhase = YearPhase.QueuedActionsEarly", plugin);
        Assert.Contains("farming.LivestockPurchasePrice", plugin);
        Assert.Contains("farmlandId", plugin);
        Assert.Contains("parcel.Town.Id.Equals(residence.Id", plugin);
        Assert.Contains("string.IsNullOrWhiteSpace(parcel.LivestockTypeId)", plugin);
        Assert.Contains("GetAvailableLivestockOptions", plugin);
        Assert.Contains("farming.AddLivestock", plugin);
    }

    [Fact]
    public void ManualFarmlandSaleUsesSelectedParcelAndLivestockSalvageWithLegacyFallback()
    {
        var plugin = Read("plugins", "Dynastia.Mechanics.Farming", "FarmingPlugin.cs");
        var app = Read("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.Actions.cs");

        Assert.Contains("ResolveFarmlandForAction", plugin);
        Assert.Contains("allowLegacyFallback: true", plugin);
        Assert.Contains("farming.GetFarmlandSaleValue(flavored)", plugin);
        Assert.Contains("\"farmlandId\"", plugin);
        Assert.Contains("? \"farmlandId\"", app);
        Assert.Contains("farming.add_livestock", app);
    }

    [Fact]
    public void VoluntaryRelocationsLiquidateOnlyOriginTownFarmsBeforeResidenceChanges()
    {
        var farming = Read("plugins", "Dynastia.Mechanics.Farming", "StandardFarmingService.cs");
        var household = Read("plugins", "Dynastia.Mechanics.Households", "HouseholdsPlugin.PropertyActions.cs");
        var marriageReconciliation = Read("plugins", "Dynastia.Mechanics.Households", "StandardHouseholdService.MembershipReconciliation.cs");
        var relations = Read("plugins", "Dynastia.Mechanics.FamilyRelations", "FamilyRelationActions.Property.cs");
        var historical = Read("plugins", "Dynastia.Mechanics.Historical", "HistoricalMigrationService.cs");

        Assert.Contains("asset.Town.Id.Equals(\n                origin.Id", farming);
        Assert.Contains("GetFarmlandSaleValue", farming);
        Assert.Contains("farming.relocation_sale", farming);

        AssertLiquidationBeforeMove(household);
        AssertLiquidationBeforeMove(relations);
        Assert.Contains("SellOriginFarmlandForVoluntaryRelocation", marriageReconciliation);
        Assert.Contains("transferredOrigin", marriageReconciliation);

        Assert.DoesNotContain("SellOriginFarmlandForVoluntaryRelocation", historical);
        Assert.Contains("replacementFarmland", historical);
        Assert.Contains("_farming?.EnsureFarmlandFlavor", historical);
    }

    [Fact]
    public void FamilyPropertiesShowsFlavorLivestockSaleValueAndParcelActions()
    {
        var viewModel = Read("src", "Dynastia.App", "ViewModels", "FamilyInventoryViewModels.cs");
        var xaml = Read("src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml");
        var code = Read("src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml.cs");

        Assert.Contains("FarmTypeEmoji", viewModel);
        Assert.Contains("FarmTypeDisplayName", viewModel);
        Assert.Contains("LivestockDisplayName", viewModel);
        Assert.Contains("Sale value", viewModel);
        Assert.Contains("AddLivestockActionText", viewModel);
        Assert.Contains("OnAddLivestockClick", xaml);
        Assert.Contains("OnSellFarmlandClick", xaml);
        Assert.DoesNotContain("OnSellFarmlandParcelClick", xaml);
        Assert.Contains("farming.add_livestock", code);
        Assert.Contains("farming.sell_farmland", code);
        Assert.Contains("farmlandId.ToString()", code);
    }

    private static void AssertLiquidationBeforeMove(string source)
    {
        var liquidation = source.IndexOf("SellOriginFarmlandForVoluntaryRelocation", StringComparison.Ordinal);
        var move = source.IndexOf("SetResidenceTown(head, destination)", liquidation, StringComparison.Ordinal);
        Assert.True(liquidation >= 0 && move > liquidation);
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static List<string[]> ReadCsv(params string[] parts) =>
        File.ReadAllLines(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.TrimStart('\uFEFF').Split(','))
            .ToList();

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
