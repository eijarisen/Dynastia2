using Dynastia.Contracts;
using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed partial class AutonomousStrategyCharacterizationTests
{
    [Fact]
    public void ResidenceExtensionSupportsContinuityDespiteExistingChildrenAndCrowding()
    {
        using var f = new Fixture();
        var house = new HousePropertyInfo(f.World.NextId(), f.World.Town, true, false,
            PurchasePrice: 10000m, BaseResidentCapacity: 6);
        var snapshot = f.Snapshot with
        {
            Finance = f.Snapshot.Finance! with { Houses = [house] },
            Status = new HouseholdStatusSnapshot(f.Head.Id, 3, 3, 3, null, null,
                false, true, true, false, false, [], ResidentCount: 7,
                OvercrowdingThreshold: 6, IsOvercrowded: true),
            NeedsMaleLineContinuity = true,
            HasRealisticReproductivePath = true,
            LivingChildCount = 3,
            FinancialState = AutonomousFinancialState.Stable
        };
        var candidate = Candidate("household.extend_house", f.Head) with
        { Parameters = new Dictionary<string, string> { ["propertyId"] = house.Id.ToString() } };
        var scorer = new AutonomousFinancePropertyScorer(f.Context, f.World.State, f.World.Economy);
        var result = Assert.IsType<AutonomousActionCandidate>(scorer.Score(candidate, snapshot));
        Assert.Equal(AutonomousPriorityBands.MaleLineContinuity, result.PriorityBand);
        Assert.Null(scorer.Score(candidate, snapshot with
        { Finance = snapshot.Finance with { Wealth = 4000m } }));
        Assert.Null(scorer.Score(candidate, snapshot with
        { Finance = snapshot.Finance with { Houses = [house with { IsResidence = false }] } }));
    }

    [Fact]
    public void HousePurchaseReservesUseTheSelectedOfferPrice()
    {
        using var f = new Fixture();
        var scorer = new AutonomousFinancePropertyScorer(f.Context, f.World.State, f.World.Economy);
        var snapshot = f.Snapshot with { HasResidence = false };
        var candidate = Candidate("household.buy_house", f.Head) with
        { Parameters = new Dictionary<string, string> { ["houseAskingPrice"] = "18001" } };
        Assert.Null(scorer.Score(candidate, snapshot));
        Assert.NotNull(scorer.Score(candidate with
        { Parameters = new Dictionary<string, string> { ["houseAskingPrice"] = "17900" } }, snapshot));
    }

    [Fact]
    public void RentedFamilyCanBuyAHouseAsTheFirstStepOfAnAffordableExtensionPlan()
    {
        using var f = new Fixture();
        var scorer = new AutonomousFinancePropertyScorer(f.Context, f.World.State, f.World.Economy);
        var snapshot = f.Snapshot with
        {
            HasResidence = false, NeedsMaleLineContinuity = true,
            HasRealisticReproductivePath = true,
            Finance = f.Snapshot.Finance! with { Wealth = 14500m },
            Status = new HouseholdStatusSnapshot(f.Head.Id, 2, 3, 3, null, null,
                false, false, false, false, false, [], ResidentCount: 8,
                OvercrowdingThreshold: 8)
        };
        var candidate = Candidate("household.buy_house", f.Head) with
        { Parameters = new Dictionary<string, string>
            { ["houseAskingPrice"] = "10000", ["houseCapacity"] = "8" } };
        var result = Assert.IsType<AutonomousActionCandidate>(scorer.Score(candidate, snapshot));
        Assert.Equal(AutonomousPriorityBands.MaleLineContinuity, result.PriorityBand);
        Assert.Null(scorer.Score(candidate, snapshot with
        { Finance = snapshot.Finance with { Wealth = 14499m } }));
        Assert.Null(scorer.Score(candidate with
        { Parameters = new Dictionary<string, string>
            { ["houseAskingPrice"] = "5000", ["houseCapacity"] = "6" } }, snapshot));
    }

    [Fact]
    public void FertilityTreatmentCanRestoreAnInfertileMaleLineAndKeepsCashReserve()
    {
        using var f = new Fixture();
        f.Context.AddService<IFamilyService>(f.World.Family);
        var snapshot = f.Snapshot with
        {
            Finance = f.Snapshot.Finance! with { Wealth = 50000m },
            NeedsMaleLineContinuity = true,
            HasRealisticReproductivePath = false,
            Members = [Member(f.Head) with
            { IsMaleLineage = true, IsBloodline = true, Stats = new Dictionary<string, int> { ["fertility"] = 0 } }]
        };
        var definition = new GameActionDefinition
        {
            Id = "stats.improve_fertility", Label = "Treatment", Description = "Treatment",
            DisplayCost = 20000m, Execute = _ => new GameActionResult(true)
        };
        var candidate = Candidate(definition.Id, f.Head) with { Action = definition };
        var scorer = new AutonomousPersonalDevelopmentScorer(f.Context);
        var result = Assert.IsType<AutonomousActionCandidate>(scorer.Score(candidate, snapshot));
        Assert.Equal(AutonomousPriorityBands.MaleLineContinuity, result.PriorityBand);
        Assert.Null(scorer.Score(candidate, snapshot with
        { Finance = snapshot.Finance with { Wealth = 23000m } }));
    }

    [Fact]
    public void SurvivalStatTreatmentRemainsUsefulWhileFamilyContinuityIsUnsecured()
    {
        using var f = new Fixture();
        var snapshot = f.Snapshot with
        {
            NeedsMaleLineContinuity = true, HasRealisticReproductivePath = true,
            Members = [Member(f.Head) with { IsMaleLineage = true, IsBloodline = true }]
        };
        var scorer = new AutonomousPersonalDevelopmentScorer(f.Context);
        var result = Assert.IsType<AutonomousActionCandidate>(
            scorer.Score(Candidate("stats.improve_immunity", f.Head), snapshot));
        Assert.Equal(AutonomousPriorityBands.FamilyStability, result.PriorityBand);
        Assert.Null(scorer.Score(Candidate("stats.improve_appeal", f.Head), snapshot));
    }

    [Fact]
    public void GivingMoneySupportsANeedyDescendantAndStopsAfterItsReserveIsFunded()
    {
        using var f = new Fixture();
        f.Context.AddService<IEconomyService>(f.World.Economy);
        var child = f.World.Person(22);
        f.World.Household(child);
        var snapshot = f.Snapshot with { LivingMaleLineDescendants = [child] };
        var candidate = Candidate("family_relations.give_money", child) with
        { Parameters = new Dictionary<string, string> { ["amount"] = "1000" } };
        var scorer = new AutonomousFamilyRelationsScorer(f.Context);
        var result = Assert.IsType<AutonomousActionCandidate>(scorer.Score(candidate, snapshot));
        Assert.Equal(AutonomousPriorityBands.MaleLineContinuity, result.PriorityBand);
        f.World.Economy.SetWealth(child, 10000m);
        Assert.Null(scorer.Score(candidate, snapshot));
        Assert.Null(scorer.Score(candidate with { Target = f.Head }, snapshot));
    }

    [Fact]
    public void MovingAResidentRequiresHousingAndIncomeInBothHouseholds()
    {
        using var f = new Fixture();
        f.Context.AddService<IFamilyService>(f.World.Family);
        var heir = f.World.Person(22);
        var house = new HousePropertyInfo(f.World.NextId(), f.World.Town, false, true,
            PurchasePrice: 10000m);
        var snapshot = f.Snapshot with
        {
            Finance = f.Snapshot.Finance! with { Houses = [house] },
            ProjectedIncome = 20000m,
            Members = [Member(f.Head), Member(heir) with
            { IsMaleLineage = true, IsBloodline = true, Career = EmployedCareer() with { AnnualIncome = 10000m } }]
        };
        var scorer = new AutonomousFinancePropertyScorer(f.Context, f.World.State, f.World.Economy);
        var candidate = Candidate("household.ask_move_out", heir);
        Assert.Null(scorer.Score(candidate, snapshot));
        candidate = candidate with
        { Parameters = new Dictionary<string, string> { ["propertyId"] = house.Id.ToString() } };
        Assert.NotNull(scorer.Score(candidate, snapshot));
        Assert.Null(scorer.Score(candidate, snapshot with { ProjectedIncome = 10000m }));
        Assert.Null(scorer.Score(candidate, snapshot with { Members = [Member(f.Head), Member(heir)] }));
    }

    [Fact]
    public void OverworkDoesNotTradeContinuityOrChildWellbeingForPromotion()
    {
        using var f = new Fixture();
        var scorer = new AutonomousCareerEducationScorer(f.Context, null!, null!);
        var snapshot = f.Snapshot with
        { Members = [Member(f.Head) with { Career = EmployedCareer() }] };
        var candidate = Candidate("career.work_harder", f.Head);
        Assert.NotNull(scorer.Score(candidate, snapshot));
        Assert.Null(scorer.Score(candidate, snapshot with { NeedsMaleLineContinuity = true }));
        Assert.Null(scorer.Score(candidate, snapshot with { DependentChildCount = 1 }));
        Assert.Null(scorer.Score(candidate, snapshot with
        { Members = [Member(f.Head, 85) with { Career = EmployedCareer() }] }));
    }

    [Fact]
    public void PaidEducationKeepsTwoYearsOfExpensesAfterTuition()
    {
        using var f = new Fixture();
        var scorer = new AutonomousCareerEducationScorer(f.Context, null!, null!);
        var snapshot = f.Snapshot with
        { Finance = f.Snapshot.Finance! with { Wealth = 8000m } };
        Assert.Null(scorer.Score(Candidate("education.get_education", f.Head), snapshot));
        Assert.NotNull(scorer.Score(Candidate("education.get_education", f.Head),
            snapshot with { Finance = snapshot.Finance with { Wealth = 9000m } }));
    }
}
