namespace Dynastia.Core.Tests;

public sealed class Development13ApprovedFixesTests
{
    [Fact]
    public void AppInstallsTheTurnStartActionSystem()
    {
        var app = RepositoryFiles.ReadText("src", "Dynastia.App", "App.axaml.cs");
        Assert.Contains("new TurnStartQueuedActionYearSystem(actionRegistry)", app);
    }

    [Fact]
    public void PendingEstateDebtKeepsItsPresentationLabel()
    {
        var view = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "EconomyViewModel.cs");
        Assert.Contains("Pending inherited debt", view);
    }

    [Theory]
    [InlineData(-1.5, -2)]
    [InlineData(1.5, 2)]
    public void PendingEstateBalanceRoundsAwayFromZeroAndAccumulatesWithoutDroppingDebt(double amount, int rounded)
    {
        using var fixture = new RefactorFixture();
        var heir = fixture.Person(12);
        fixture.Economy.SetPendingInheritance(heir, (decimal)amount);
        Assert.Equal((decimal)rounded, fixture.Economy.GetPendingInheritance(heir));
        fixture.Economy.ChangePendingInheritance(heir, -5m);
        Assert.Equal(rounded - 5m, fixture.Economy.GetPendingInheritance(heir));
    }

    [Fact]
    public void CriminalOccupationUsesSharedProductiveEffort()
    {
        var service = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Justice", "CriminalOccupationService.cs");
        Assert.Contains("AnnualProductiveEffortRules.Get(person, _workCapacity)", service);
        Assert.Contains("if (!effort.CanProduce)", service);
        Assert.Contains("effort.Apply(CalculateIncome", service);
    }

    [Fact]
    public void PassiveIncomeCanSuppressOnlyHusbandUnemploymentPenalty()
    {
        var economy = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Economy", "StandardEconomyService.Forecast.cs");
        var marriage = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Relationships", "MarriageSatisfactionYearSystem.cs");
        Assert.Contains("HasSufficientPassiveIncomeForBasicNeeds", economy);
        Assert.Contains("GetExpectedPassiveAnnualIncome", economy);
        Assert.Contains("GetProjectedPassiveIncome", economy);
        Assert.Contains("!_economy.HasSufficientPassiveIncomeForBasicNeeds(husband)", marriage);
        Assert.Contains("UnemployedWorkingSpousePenalty", marriage);
    }

    [Fact]
    public void WeakPolicyImplementationIsNotPublishedAsNewsAndFamilyTabsMatchTownAffairs()
    {
        var policy = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Community", "CommunityPolicyService.cs");
        var relations = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "FamilyRelationsWindow.axaml");
        var town = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        Assert.Contains("!chosen.ImpactTier.Equals(\"Weak\"", policy);
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"14.5\" />", relations);
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"14.5\" />", town);
    }
}
