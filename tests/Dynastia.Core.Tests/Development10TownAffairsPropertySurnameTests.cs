using Dynastia.Contracts;

namespace Dynastia.Core.Tests;

public sealed class Development10TownAffairsPropertySurnameTests
{
    [Theory]
    [InlineData("Kowalski", "Kowalska")]
    [InlineData("Nowicki", "Nowicka")]
    [InlineData("Grodzki", "Grodzka")]
    [InlineData("Zielony", "Zielona")]
    [InlineData("Cichy", "Cicha")]
    public void PolishFemaleSurnameRulesCoverRequestedAdjectivalEndings(
        string masculine,
        string feminine)
    {
        Assert.Equal(feminine, PolishSurnameRules.Feminize(masculine));
    }

    [Fact]
    public void HouseExtensionAddsCapacityAndCapitalValue()
    {
        var house = new HousePropertyInfo(
            Guid.NewGuid(),
            new TownInfo("Test", "County", 20, 50, 1_000),
            true,
            false,
            PurchasePrice: 40_000m,
            CapacityExtensions: 2);

        Assert.Equal(10_000m, house.ExtensionCost);
        Assert.Equal(10, house.ResidentCapacity);
        Assert.Equal(20_000m, house.ImprovementValue);
    }

    [Fact]
    public void TownAffairsUsesCompactQualitativeInstitutionPresentation()
    {
        var root = RepositoryFiles.Root;
        var xaml = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "Views", "TownLifeWindow.axaml"));
        var service = File.ReadAllText(Path.Combine(
            root, "plugins", "Dynastia.Mechanics.TownLife", "StandardTownLifeService.cs"));

        Assert.Contains("VerticalAlignment=\"Center\"", xaml);
        Assert.Contains("ColumnSpacing=\"10\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasServiceText}\"", xaml);
        Assert.Contains("Text=\"Polity\"", xaml);
        Assert.True(
            xaml.IndexOf("Text=\"Polity\"", StringComparison.Ordinal)
            < xaml.IndexOf("Text=\"Region\"", StringComparison.Ordinal));
        Assert.Contains("Loan offers: favorable", service);
        Assert.Contains("Healthcare: good", service);
        Assert.DoesNotContain("PrincipalMultiplierMin:P0", service);
        Assert.DoesNotContain("TreatmentSuccessAdd:P0", service);
    }

    [Fact]
    public void CraftEducationReportsRegionalIndustrySupport()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryFiles.Root,
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.CraftsEducation.cs"));

        Assert.Contains("RegionOpportunityTags", source);
        Assert.Contains("RequiredOpportunityTags", source);
        Assert.Contains("PreferredOpportunityTags", source);
        Assert.Contains("Regional support: Yes", source);
        Assert.Contains("Regional support: No", source);
    }

    [Fact]
    public void ExtendHouseLivesInPropertyManagementAndTargetsAnyOwnedHouse()
    {
        var root = RepositoryFiles.Root;
        var actions = File.ReadAllText(Path.Combine(
            root, "plugins", "Dynastia.Mechanics.Households", "HouseholdsPlugin.PropertyActions.cs"));
        var inventory = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml"));
        var capacity = File.ReadAllText(Path.Combine(
            root, "plugins", "Dynastia.Mechanics.Economy", "StandardEconomyService.Capacity.cs"));

        Assert.Contains("economy.GetHouses(context.Actor)", actions);
        Assert.Contains("householdCapacity.ExtendHouse", actions);
        Assert.Contains("GetHouseValue(updated)", actions);
        Assert.Contains("OnExtendHouseClick", inventory);
        Assert.Contains("Content=\"{Binding ExtendActionText}\"", inventory);
        Assert.Contains("public bool ExtendHouse", capacity);
    }

}
