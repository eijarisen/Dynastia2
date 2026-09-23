using Dynastia.Mechanics.Relationships;

namespace Dynastia.Core.Tests;

public sealed class Development11EmploymentEducationInventoryTests
{
    [Fact]
    public void FarmWorkAndCraftSelfEmploymentCountAsEmploymentWhereItMatters()
    {
        var marriage = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Relationships", "MarriageSatisfactionYearSystem.cs");
        var career = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Career", "StandardCareerService.cs");

        Assert.Contains("IsEconomicallyEmployed", marriage);
        Assert.Contains("_farming.IsWorkingFarmWorker(person, person)", marriage);
        Assert.Contains("career.IsEmployed", marriage);

        Assert.Contains("|| HasCraftOccupation(person)", career);
        Assert.Contains("career.JobLevel > 0 || isCraftSelfEmployed", career);
    }

    [Fact]
    public void FarmWorkSuppressesUnemploymentThoughtsAndStaleMarriageIssueText()
    {
        var careerThoughts = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Thoughts", "CareerThoughtProvider.cs");
        var relationshipThoughts = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Thoughts", "RelationshipThoughtProvider.cs");

        var farmWork = careerThoughts.IndexOf(
            "_farming.IsWorkingFarmWorker(person, householdHead)",
            StringComparison.Ordinal);
        var recentJobLoss = careerThoughts.IndexOf(
            "recent.job_loss",
            farmWork,
            StringComparison.Ordinal);

        Assert.True(farmWork >= 0 && recentJobLoss > farmWork);
        Assert.Contains("career.farm_work", careerThoughts);
        Assert.Contains("\"🌾\"", careerThoughts);
        Assert.Contains("IsIssueStillCurrent", relationshipThoughts);
        Assert.Contains("_farming.IsWorkingFarmWorker(person, person)", relationshipThoughts);
    }

    [Fact]
    public void EducationKeepsStandardFirstAndPrioritizesLocallySupportedCrafts()
    {
        var education = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.CraftsEducation.cs");

        var standard = education.IndexOf("\"Standard Education\"", StringComparison.Ordinal);
        var craftList = education.IndexOf("var craftOptions", StringComparison.Ordinal);

        Assert.True(standard >= 0 && craftList > standard);
        Assert.Contains("OrderByDescending(option => option.HasRegionalSupport)", education);
        Assert.Contains("ThenByDescending(option => option.IsKnownCraft)", education);
    }

    [Fact]
    public void InventoryUsesCompactInheritanceFarmlandSelectorAndCenteredLifestyleButtons()
    {
        var xaml = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml");
        var code = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml.cs");

        Assert.Contains("<Setter Property=\"Width\" Value=\"180\" />", xaml);
        Assert.Contains("Content=\"💎 Lavish\"", xaml);
        Assert.Contains("Content=\"⚖️ Balanced\"", xaml);
        Assert.Contains("Content=\"🪙 Thrifty\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Center\"", xaml);

        Assert.Contains("Click=\"OnBuyFarmlandClick\"", xaml);
        Assert.Contains("Click=\"OnSellFarmlandClick\"", xaml);
        Assert.DoesNotContain("OnSellFarmlandParcelClick", xaml);
        Assert.Contains("OpenPropertyWindowAndClose(", code);
        Assert.Contains("Close();", code);
        Assert.Contains("\"farming.sell_farmland\"", code);
        Assert.Contains("\"Select Farmland\"", code);
    }

    [Fact]
    public void NewMarriageTraitThresholdsMatchRequestedBalance()
    {
        Assert.Equal(0, MarriageBalanceRules.GetLowAttractionPenalty(1, 1), 10);
        Assert.Equal(0, MarriageBalanceRules.GetLowAttractionPenalty(2, 1), 10);
        Assert.True(MarriageBalanceRules.GetLowAttractionPenalty(3, 1) > 0);
        Assert.Equal(0, MarriageBalanceRules.GetLowAttractionPenalty(5, 1, wifeRetired: true), 10);

        Assert.True(MarriageBalanceRules.ShouldApplyLowFertilityPenalty(40, 1));
        Assert.False(MarriageBalanceRules.ShouldApplyLowFertilityPenalty(41, 1));
        Assert.True(MarriageBalanceRules.ShouldApplyLowIntellectPenalty(1));
        Assert.False(MarriageBalanceRules.ShouldApplyLowIntellectPenalty(2));
    }

}
