using Dynastia.Contracts;
using Dynastia.Core.Entities;

namespace Dynastia.Core.Tests;

public sealed class Development10LifestyleAssimilationTests
{
    [Fact]
    public void LifestyleSpendingUsesRequestedLivingCostEnvelope()
    {
        Assert.Equal(1.10m, HouseholdLifestyleRules.GetLivingCostMultiplier(HouseholdLifestyleStance.Lavish));
        Assert.Equal(1.00m, HouseholdLifestyleRules.GetLivingCostMultiplier(HouseholdLifestyleStance.Balanced));
        Assert.Equal(0.90m, HouseholdLifestyleRules.GetLivingCostMultiplier(HouseholdLifestyleStance.Thrifty));
    }

    [Fact]
    public void LifestyleSecondaryEffectsStaySubtleAndSymmetric()
    {
        var lavish = new Person("Lavish", "Test", 30);
        lavish.Tags.Add(HouseholdLifestyleRules.LavishTag);
        var thrifty = new Person("Thrifty", "Test", 30);
        thrifty.Tags.Add(HouseholdLifestyleRules.ThriftyTag);

        Assert.Equal(0.25, HouseholdLifestyleRules.GetHealthRegenerationModifier(lavish), 6);
        Assert.Equal(-0.25, HouseholdLifestyleRules.GetHealthRegenerationModifier(thrifty), 6);
        Assert.Equal(1.05, HouseholdLifestyleRules.GetEducationChanceMultiplier(lavish), 6);
        Assert.Equal(0.95, HouseholdLifestyleRules.GetEducationChanceMultiplier(thrifty), 6);
        Assert.Equal(1.05, HouseholdLifestyleRules.GetPartnerChanceMultiplier(lavish), 6);
        Assert.Equal(0.95, HouseholdLifestyleRules.GetPartnerChanceMultiplier(thrifty), 6);
        Assert.Equal(0.25, HouseholdLifestyleRules.GetMarriageSatisfactionModifier(lavish), 6);
        Assert.Equal(-0.25, HouseholdLifestyleRules.GetMarriageSatisfactionModifier(thrifty), 6);
        Assert.Equal(-0.25, HouseholdLifestyleRules.GetStressAdjustment(lavish), 6);
        Assert.Equal(0.25, HouseholdLifestyleRules.GetStressAdjustment(thrifty), 6);
        Assert.Equal(0.05, HouseholdLifestyleRules.GetMoraleShiftChance(lavish), 6);
        Assert.Equal(0.05, HouseholdLifestyleRules.GetMoraleShiftChance(thrifty), 6);
    }

    [Fact]
    public void TownAffairsSupportsAdultHouseholdResidentsAndKeepsInstitutionGrid()
    {
        var root = RepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs"));
        var window = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "Views", "TownLifeWindow.axaml"));

        Assert.Contains("target.Age < 18", viewModel);
        Assert.Contains("GetHouseholdMemberIds(actor)", viewModel);
        Assert.DoesNotContain("_succession.IsControllable(target)", viewModel);
        Assert.Contains("UniformGrid Columns=\"4\" Rows=\"2\"", window);
        Assert.Contains("<TabControl", window);
        Assert.Contains("<ScrollViewer", window);
    }

    [Fact]
    public void HouseholdBudgetGraphLivesInPaperMenuAndLifestyleButtonsExplainAndClose()
    {
        var root = RepositoryRoot();
        var main = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "Views", "MainWindow.axaml"));
        var inventory = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml"));
        var inventoryCodeBehind = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml.cs"));
        var actions = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.Actions.cs"));

        Assert.DoesNotContain("HouseholdBudgetGraph", main);
        Assert.Contains("HouseholdBudgetText", main);
        Assert.Contains("HouseholdLastYearText", main);
        Assert.Contains("HouseholdBudgetGraph", inventory);
        Assert.Contains("BudgetHistory", inventory);
        Assert.Contains("Lifestyle Spending", inventory);
        Assert.Contains("economy.lifestyle.lavish", inventory);
        Assert.Contains("economy.lifestyle.balanced", inventory);
        Assert.Contains("economy.lifestyle.thrifty", inventory);
        Assert.Contains("+10% living costs", inventory);
        Assert.Contains("Standard living costs", inventory);
        Assert.Contains("-10% living costs", inventory);
        Assert.Contains("if (!result.Success)", inventoryCodeBehind);
        Assert.Contains("Close();", inventoryCodeBehind);
        Assert.Contains("economy.lifestyle.", actions);
    }

    [Fact]
    public void PsychotherapyIsPresentedThroughTownAffairsHealthWithDynamicPrice()
    {
        var root = RepositoryRoot();
        var selfImprovement = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.SelfImprovement.cs"));
        var townAffairs = File.ReadAllText(Path.Combine(
            root, "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs"));
        var treatment = File.ReadAllText(Path.Combine(
            root, "plugins", "Dynastia.Mechanics.Wellbeing", "WellbeingPlugin.TreatmentActions.cs"));

        Assert.DoesNotContain("TherapyActionId", selfImprovement);
        Assert.Contains("wellbeing.therapy", townAffairs);
        Assert.Contains("GetCandidateActions(actor, subject)", townAffairs);
        Assert.Contains("TownAffairsHealthActionIds.Contains(action.Id)", townAffairs);
        Assert.Contains("item.Action.Label", townAffairs);
        Assert.Contains("item.Action.DisplayCost", townAffairs);
        Assert.Contains("DisplayCost = treatmentCost", treatment);
        Assert.Contains("RegisterDynamicProvider", treatment);
    }

    [Fact]
    public void AssimilationIsRareAndPolishNamingPersistsIntoNewborns()
    {
        var root = RepositoryRoot();
        var assimilation = File.ReadAllText(Path.Combine(
            root, "plugins", "Dynastia.Mechanics.Households", "AssimilationYearSystem.cs"));
        var surnameAction = File.ReadAllText(Path.Combine(
            root, "plugins", "Dynastia.Mechanics.Households", "HouseholdsPlugin.Assimilation.cs"));
        var reproduction = File.ReadAllText(Path.Combine(
            root, "plugins", "Dynastia.Mechanics.Reproduction", "ReproductionYearSystem.cs"));

        Assert.Contains("AnnualAssimilationChance = 0.005", assimilation);
        Assert.Contains("_economy.GetHouseholdId(person) is not null", assimilation);
        Assert.Contains("SetNationality", assimilation);
        Assert.Contains("PolishNamingTag", assimilation);
        Assert.Contains("family.adopt_polish_surname", surnameAction);
        Assert.Contains("PolishSurnameAdoptedTag", surnameAction);
        Assert.Contains("gameState.DynastySurname = newSurname", surnameAction);
        Assert.Contains("UsesPolishNaming(father, mother)", reproduction);
        Assert.Contains("? \"polish\"", reproduction);
    }

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
