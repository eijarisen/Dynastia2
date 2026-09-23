namespace Dynastia.Core.Tests;

public sealed class Development13ApprovedFixesTests
{
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

    [Fact]
    public void TurnStartActionsRunBeforeAgingAndConsumeCommittedQueue()
    {
        Assert.True(Dynastia.Contracts.YearPhase.TurnStartActions < Dynastia.Contracts.YearPhase.Aging);
        var registry = Read("src", "Dynastia.Core", "Actions", "ActionRegistry.cs");
        var app = Read("src", "Dynastia.App", "App.axaml.cs");
        Assert.Contains("ExecuteTurnStartQueuedActions", registry);
        Assert.Contains("committedAtTurnStart: true", registry);
        Assert.Contains("new TurnStartQueuedActionYearSystem(actionRegistry)", app);
        Assert.Contains("schedulingPreview ? _gameState.Year + 1", registry);
    }

    [Fact]
    public void SignedEstateDebtIsPreservedAndPresentedAsDebt()
    {
        var estate = Read("plugins", "Dynastia.Mechanics.Inheritance", "EstateInheritanceSystem.cs");
        var assets = Read("plugins", "Dynastia.Mechanics.Economy", "StandardEconomyService.Assets.cs");
        var adulthood = Read("plugins", "Dynastia.Mechanics.Inheritance", "AdulthoodInheritanceSystem.cs");
        var view = Read("src", "Dynastia.App", "ViewModels", "EconomyViewModel.cs");
        Assert.Contains("var sign = Math.Sign(wholeUnits);", estate);
        Assert.Contains("ChangeWealthAllowDebt(heir, amount)", estate);
        Assert.Contains("RoundCurrency(amount)", assets);
        Assert.Contains("ChangeWealthAllowDebt", adulthood);
        Assert.Contains("Pending inherited debt", view);
    }

    [Fact]
    public void CriminalOccupationUsesSharedProductiveEffort()
    {
        var service = Read("plugins", "Dynastia.Mechanics.Justice", "CriminalOccupationService.cs");
        Assert.Contains("AnnualProductiveEffortRules.Get(person, _workCapacity)", service);
        Assert.Contains("if (!effort.CanProduce)", service);
        Assert.Contains("effort.Apply(CalculateIncome", service);
    }

    [Fact]
    public void PassiveIncomeCanSuppressOnlyHusbandUnemploymentPenalty()
    {
        var economy = Read("plugins", "Dynastia.Mechanics.Economy", "StandardEconomyService.Forecast.cs");
        var marriage = Read("plugins", "Dynastia.Mechanics.Relationships", "MarriageSatisfactionYearSystem.cs");
        Assert.Contains("HasSufficientPassiveIncomeForBasicNeeds", economy);
        Assert.Contains("GetExpectedPassiveAnnualIncome", economy);
        Assert.Contains("GetProjectedPassiveIncome", economy);
        Assert.Contains("!_economy.HasSufficientPassiveIncomeForBasicNeeds(husband)", marriage);
        Assert.Contains("UnemployedWorkingSpousePenalty", marriage);
    }

    [Fact]
    public void WeakPolicyImplementationIsNotPublishedAsNewsAndFamilyTabsMatchTownAffairs()
    {
        var policy = Read("plugins", "Dynastia.Mechanics.Community", "CommunityPolicyService.cs");
        var relations = Read("src", "Dynastia.App", "Views", "FamilyRelationsWindow.axaml");
        var town = Read("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        Assert.Contains("!chosen.ImpactTier.Equals(\"Weak\"", policy);
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"14.5\" />", relations);
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"14.5\" />", town);
    }
}
