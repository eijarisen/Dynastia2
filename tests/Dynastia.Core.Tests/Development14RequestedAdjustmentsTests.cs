using Dynastia.Contracts;
using Dynastia.Core.Entities;
using Dynastia.Mechanics.Farming;
using Dynastia.Mechanics.Health;

namespace Dynastia.Core.Tests;

public sealed class Development14RequestedAdjustmentsTests
{
    [Fact]
    public void StressScaleIsZeroToOneHundredAndPreservesLegacyReactionBalance()
    {
        Assert.Equal(100d, StressScale.Maximum);
        Assert.Equal(100d, StandardStressService.MaximumStress);
        Assert.Equal(60d, StressScale.FromLegacy(6));
        Assert.Equal(6d, StressScale.ToLegacy(60));

        var person = new Person("Jan", "Test", 30);
        Assert.Equal(
            0.0015 + 6 * 0.011,
            MentalHealthStressRules.GetReactionChance(60, person, 0),
            10);
    }

    [Fact]
    public void FarmlandMarketUsesNineToElevenThousandPriceAndCurrentTownHousingNavigation()
    {
        Assert.Equal(9_000m, FarmingRules.MinimumPurchasePrice);
        Assert.Equal(11_000m, FarmingRules.MaximumPurchasePrice);
        Assert.Equal(100m, FarmingRules.PurchasePriceStep);
        Assert.Equal(9_000m, FarmingRules.GetMarketPurchasePrice(0));
        Assert.Equal(11_000m, FarmingRules.GetMarketPurchasePrice(1));
        for (var index = 0; index <= 1000; index++)
        {
            var price = FarmingRules.GetMarketPurchasePrice(index / 1000d);
            Assert.InRange(price, 9_000m, 11_000m);
            Assert.Equal(0m, (price - 9_000m) % 100m);
        }

        var inventoryCode = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml.cs");
        var townLife = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var farming = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Farming", "FarmingPlugin.cs");
        var townWindow = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        var start = inventoryCode.IndexOf("private async void OnBuyFarmlandClick", StringComparison.Ordinal);
        var end = inventoryCode.IndexOf("private async void OnSellFarmlandClick", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var buyFarmland = inventoryCode[start..end];
        Assert.Contains("CreateActiveHouseholdTownAffairsRequest", buyFarmland);
        Assert.Contains("TownAffairsTab.Housing", buyFarmland);
        Assert.DoesNotContain("QueueFamilyInventoryAction", buyFarmland);

        Assert.Contains("_farmingService.GetPurchasePrice(town, _gameState.Year)", townLife);
        Assert.Contains("farmlandOfferYear", townLife);
        Assert.Contains("farmlandAskingPrice", townLife);
        Assert.Contains("farming.GetPurchasePrice(town, offerYear)", farming);
        Assert.Contains("ItemsSource=\"{Binding FarmlandOffers}\"", townWindow);
        Assert.Contains("OnBuyFarmlandOfferClick", townWindow);
    }

    [Fact]
    public void RequestedCompactUiLayoutsRemainCollapsedAndTight()
    {
        var partners = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "PotentialPartnersWindow.axaml");
        Assert.DoesNotContain("Height=\"258\"", partners);
        Assert.Contains("RowDefinitions=\"Auto,Auto\" RowSpacing=\"4\"", partners);

        var inventory = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml");
        Assert.Contains("IsVisible=\"{Binding ShowHouseEmpty}\"", inventory);
        Assert.Contains("IsVisible=\"{Binding ShowFarmlandEmpty}\"", inventory);
        Assert.Contains("IsVisible=\"{Binding ShowHeirloomEmpty}\"", inventory);

        var loans = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "LoanSelectionWindow.axaml");
        Assert.DoesNotContain("FontSize=\"15.5\"", loans);
        Assert.Contains("VerticalAlignment=\"Center\"", loans);
        Assert.Contains("Margin=\"5,3\"", loans);
        Assert.Contains("Padding=\"14,9\"", loans);
    }

    [Fact]
    public void AcquaintancesUseStatusWordsInsteadOfNumericRenownAndReputation()
    {
        var relations = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.Relations.cs");

        Assert.Contains("GetRenownLabel(connection.Renown)", relations);
        Assert.Contains("GetReputationLabel(connection.Reputation)", relations);
        Assert.Contains("$\"{renownLabel} as {reputationLabel}\"", relations);
        Assert.DoesNotContain("Renown {connection.Renown", relations);
        Assert.DoesNotContain("Reputation {connection.Reputation", relations);
    }
    [Fact]
    public void TownAffairsStatusInstitutionLoanAndPropertyPresentationMatchesRequestedLayout()
    {
        var hub = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");
        var townLife = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var window = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var loanWindow = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "LoanSelectionWindow.axaml");
        var propertyWindow = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "PropertySelectionWindow.axaml");
        var options = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "Actions", "ActionSelectionOptionService.cs");
        var familyCard = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "FamilyMemberCardViewModel.cs");
        var familyWindow = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "MainWindow.axaml");
        var genealogy = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Genealogy", "Host", "GameGenealogyDataSource.cs");

        Assert.Contains("GetTownAffairsRenownLabel(CivicOffice.Renown)", hub);
        Assert.Contains("GetTownAffairsReputationLabel(CivicOffice.Reputation)", hub);
        Assert.Contains("GetRenownLabel(value)", townLife);
        Assert.Contains("GetReputationLabel(value)", townLife);
        Assert.Contains("HousingFundsText", window);
        Assert.Contains("Snapshot.Medical.DisplayName", window);
        Assert.Contains("Snapshot.Church.DisplayName", window);
        Assert.Contains("Snapshot.School.DisplayName", window);
        Assert.Contains("Snapshot.Bank.DisplayName", window);
        Assert.Contains("Foreground=\"{Binding BankStatusBrush}\"", window);
        Assert.Contains("Text=\"{Binding Description}\"", window);
        Assert.DoesNotContain("Click=\"OnChurchActionClick\" ToolTip.Tip=\"{Binding Description}\"", window);
        Assert.DoesNotContain("Text=\"{Binding FavorabilityText}\"", loanWindow);
        Assert.Contains("Foreground=\"{Binding FavorabilityBrush}\"", loanWindow);
        Assert.Contains("FontSize=\"{Binding LeadingEmojiFontSize}\"", propertyWindow);
        Assert.Contains("LeadingEmoji: \"🏠\"", options);
        Assert.Contains("LeadingEmojiFontSize: 32", options);
        Assert.Contains("RenownStatusText", familyCard);
        Assert.Contains("ReputationStatusText", familyCard);
        Assert.Contains("Foreground=\"{Binding RenownStatusBrush}\"", familyWindow);
        Assert.Contains("Foreground=\"{Binding ReputationStatusBrush}\"", familyWindow);
        Assert.Contains("$\"Renown: {status.RenownLabel}\"", genealogy);
        Assert.Contains("$\"Reputation: {status.ReputationLabel}\"", genealogy);
    }

}
