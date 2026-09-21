using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed class Development10HouseholdFamilyBalanceTests
{
    [Fact]
    public void OvercrowdingScalesPerResidentAboveCapacity()
    {
        Assert.False(HouseholdCrowdingRules.IsOvercrowded(8));
        Assert.True(HouseholdCrowdingRules.IsOvercrowded(9));
        Assert.Equal(1, HouseholdCrowdingRules.GetResidentsAboveCapacity(9));
        Assert.Equal(3, HouseholdCrowdingRules.GetResidentsAboveCapacity(11));
        Assert.Equal(0.75, HouseholdCrowdingRules.GetAnnualHealthPenalty(9), 10);
        Assert.Equal(2.25, HouseholdCrowdingRules.GetAnnualHealthPenalty(11), 10);
        Assert.Equal(1.0, HouseholdCrowdingRules.GetAnnualStressPenalty(9), 10);
        Assert.Equal(3.0, HouseholdCrowdingRules.GetAnnualStressPenalty(11), 10);
    }

    [Fact]
    public void AdultHouseholdResidentsAreNotAutomaticallyMadeIndependentAtEighteen()
    {
        var source = ReadRepositoryFile(
            "plugins",
            "Dynastia.Mechanics.Adoption",
            "AdoptionYearSystem.cs");

        Assert.Contains(
            "previousKind != AdoptionPlacementKind.Orphanage",
            source);
        Assert.Contains(
            "Sons and daughters remain resident until",
            source);
        Assert.DoesNotContain(
            "Every adult male bloodline member becomes",
            source);
    }

    [Fact]
    public void RecoverReducesJobsCraftsAndFarmingOutput()
    {
        var career = ReadRepositoryFile(
            "plugins",
            "Dynastia.Mechanics.Career",
            "CareerIncomeProvider.cs");
        var crafts = ReadRepositoryFile(
            "plugins",
            "Dynastia.Mechanics.Crafts",
            "StandardCraftService.cs");
        var farming = ReadRepositoryFile(
            "plugins",
            "Dynastia.Mechanics.Farming",
            "StandardFarmingService.cs");

        Assert.Contains("modifier.salary.recover.", career);
        Assert.Contains("modifier.salary.recover.", crafts);
        Assert.Contains("ApplyRecoverReduction", farming);
        Assert.Contains("modifier.salary.recover.", farming);
    }

    [Fact]
    public void HouseholdFormationCarriesFinalChildhoodHappinessIntoFamilyRelations()
    {
        var bridge = ReadRepositoryFile(
            "plugins",
            "Dynastia.Mechanics.FamilyRelations",
            "FamilyRelationEventBridge.cs");

        Assert.Contains("household.member_moved_out", bridge);
        Assert.Contains("GetFinalChildhoodHappiness", bridge);
        Assert.Contains("family_relations.childhood_handoff_applied", bridge);
        Assert.Contains("GetSiblings(adultChild)", bridge);
    }

    [Fact]
    public void DesignatedInheritanceCanCreateRelationshipFallout()
    {
        var inheritance = ReadRepositoryFile(
            "plugins",
            "Dynastia.Mechanics.Inheritance",
            "EstateInheritanceSystem.cs");
        var bridge = ReadRepositoryFile(
            "plugins",
            "Dynastia.Mechanics.FamilyRelations",
            "FamilyRelationEventBridge.cs");

        Assert.Contains("inheritance.disadvantaged", inheritance);
        Assert.Contains("hasExplicitDesignation", inheritance);
        Assert.Contains("_economy.GetHouseValue", inheritance);
        Assert.Contains("_farmingResolver()?.PurchasePrice", inheritance);
        Assert.Contains("AppraisedValue", inheritance);
        Assert.Contains("receivedAssetValue", inheritance);
        Assert.Contains("favoredAssetValue", inheritance);
        Assert.Contains("favoredHeirIds", inheritance);
        Assert.Contains("receivedValue * 2m < maximum", inheritance);
        Assert.Contains("inheritance.disadvantaged", bridge);
        Assert.Contains("ApplyInheritanceDisadvantage", bridge);
    }

    [Fact]
    public void MapDoubleClickOpensTownSpecificTownLife()
    {
        var control = ReadRepositoryFile(
            "src",
            "Dynastia.App",
            "Map",
            "Rendering",
            "TownMapControl.cs");
        var window = ReadRepositoryFile(
            "src",
            "Dynastia.App",
            "Map",
            "Views",
            "TownMapWindow.cs");

        Assert.Contains("e.ClickCount >= 2", control);
        Assert.Contains("TownActivated?.Invoke(hit)", control);
        Assert.Contains("GetTownLife(townId)", window);
    }

    private static string ReadRepositoryFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(parts)));

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
