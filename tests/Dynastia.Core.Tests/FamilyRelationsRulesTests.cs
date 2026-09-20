using Dynastia.Contracts;
using Dynastia.Mechanics.FamilyRelations;

namespace Dynastia.Core.Tests;

public sealed class FamilyRelationsRulesTests
{
    [Theory]
    [InlineData(0, "Distant")]
    [InlineData(24, "Distant")]
    [InlineData(25, "Known")]
    [InlineData(50, "Familiar")]
    [InlineData(75, "Close")]
    [InlineData(100, "Close")]
    public void FamiliarityLabelsFollowSpecification(double score, string expected)
    {
        Assert.Equal(expected, FamilyRelationScoreRules.GetFamiliarityState(score));
    }

    [Theory]
    [InlineData(0, "Hostile")]
    [InlineData(19, "Hostile")]
    [InlineData(20, "Cold")]
    [InlineData(40, "Neutral")]
    [InlineData(60, "Warm")]
    [InlineData(80, "Affectionate")]
    [InlineData(100, "Affectionate")]
    public void SympathyLabelsFollowSpecification(double score, string expected)
    {
        Assert.Equal(expected, FamilyRelationScoreRules.GetSympathyState(score));
    }

    [Fact]
    public void RequestsNeedBothFamiliarityAndSympathy()
    {
        var distantHostile = FamilyRelationScoreRules.GetRequestWillingness(10, 10);
        var closeHostile = FamilyRelationScoreRules.GetRequestWillingness(90, 10);
        var distantAffectionate = FamilyRelationScoreRules.GetRequestWillingness(10, 90);
        var closeAffectionate = FamilyRelationScoreRules.GetRequestWillingness(90, 90);

        Assert.Equal(0.0, distantHostile, 6);
        Assert.True(distantHostile < closeHostile);
        Assert.True(closeHostile < closeAffectionate);
        Assert.True(distantHostile < distantAffectionate);
        Assert.True(distantAffectionate < closeAffectionate);
    }

    [Fact]
    public void VeryLowSympathyCanRejectOffers()
    {
        var hostile = FamilyRelationScoreRules.GetOfferWillingness(50, 10);
        var neutral = FamilyRelationScoreRules.GetOfferWillingness(50, 50);

        Assert.InRange(hostile, 0.30, 0.70);
        Assert.Equal(1.0, neutral, 6);
    }

    [Fact]
    public void GrandparentsHaveHighestDeteriorationTolerance()
    {
        var grandparent = FamilyRelationScoreRules.GetDeteriorationMultiplier(
            FamilyRelationshipType.GrandparentGrandchild);
        var immediate = FamilyRelationScoreRules.GetDeteriorationMultiplier(
            FamilyRelationshipType.ParentChild);
        var extended = FamilyRelationScoreRules.GetDeteriorationMultiplier(
            FamilyRelationshipType.FirstCousin);

        Assert.True(grandparent < immediate);
        Assert.True(immediate < extended);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    public void CareerConnectionsAreAlwaysTwoLevelsBelowTheStrongestCurrentJob(
        int strongestJobLevel,
        int expectedPlacement)
    {
        Assert.Equal(
            expectedPlacement,
            FamilyCareerConnectionRules.GetPlacementLevel(strongestJobLevel));
        Assert.Equal(
            expectedPlacement,
            FamilyCareerConnectionRules.GetStandardPlacementLevel(strongestJobLevel));
        Assert.Equal(
            expectedPlacement,
            FamilyCareerConnectionRules.GetExceptionalPlacementLevel(strongestJobLevel));
    }

    [Fact]
    public void CareerConnectionsCannotBootstrapPastTheFixedTwoLevelCeiling()
    {
        Assert.False(FamilyCareerConnectionRules.CanProvideHelp(1));
        Assert.False(FamilyCareerConnectionRules.CanProvideHelp(2));
        Assert.True(FamilyCareerConnectionRules.CanProvideHelp(3));

        Assert.True(FamilyCareerConnectionRules.CanImprove(0, 3));
        Assert.False(FamilyCareerConnectionRules.CanImprove(1, 3));

        Assert.True(FamilyCareerConnectionRules.CanImprove(2, 5));
        Assert.False(FamilyCareerConnectionRules.CanImprove(3, 5));
        Assert.Equal(3, FamilyCareerConnectionRules.GetPlacementLevel(5));
    }

    [Fact]
    public void CareerConnectionsRequireWarmOrCloseRelations()
    {
        Assert.True(FamilySupportAbilityRules.HasStrongCareerConnectionRelation(10, 60));
        Assert.True(FamilySupportAbilityRules.HasStrongCareerConnectionRelation(75, 10));
        Assert.False(FamilySupportAbilityRules.HasStrongCareerConnectionRelation(74, 59));
    }

    [Fact]
    public void FamilyRequestsProtectEssentialAssetsAndRewardRealSurplus()
    {
        Assert.Equal(0.0, FamilySupportAbilityRules.GetMoneyRequestAbilityFactor(
            donorWealth: 2200m,
            requesterWealth: 0m,
            projectedAnnualExpenses: 2000m,
            requestedAmount: 500m,
            houseCount: 1,
            farmlandCount: 1));

        var modestCash = FamilySupportAbilityRules.GetMoneyRequestAbilityFactor(
            donorWealth: 3000m,
            requesterWealth: 1000m,
            projectedAnnualExpenses: 2000m,
            requestedAmount: 500m,
            houseCount: 1,
            farmlandCount: 1);
        var wealthyCash = FamilySupportAbilityRules.GetMoneyRequestAbilityFactor(
            donorWealth: 50000m,
            requesterWealth: 1000m,
            projectedAnnualExpenses: 2000m,
            requestedAmount: 500m,
            houseCount: 3,
            farmlandCount: 3);

        Assert.True(wealthyCash > modestCash);
        Assert.Equal(0.0, FamilySupportAbilityRules.GetHouseRequestAbilityFactor(
            10000m, 0m, 2000m, houseCount: 1, farmlandCount: 3));
        Assert.True(FamilySupportAbilityRules.GetHouseRequestAbilityFactor(
            50000m, 0m, 2000m, houseCount: 3, farmlandCount: 3) > 0);
        Assert.Equal(0.0, FamilySupportAbilityRules.GetFarmlandRequestAbilityFactor(
            10000m, 0m, 2000m, farmlandCount: 1, houseCount: 3));
        Assert.True(FamilySupportAbilityRules.GetFarmlandRequestAbilityFactor(
            50000m, 0m, 2000m, farmlandCount: 3, houseCount: 3) > 0);
    }

    [Fact]
    public void RelationsActionsRunBeforeThoughtGeneration()
    {
        Assert.True(YearPhase.MoralsReflection < YearPhase.FamilyRelations);
        Assert.True(YearPhase.FamilyRelations < YearPhase.FamilyRelationActions);
        Assert.True(YearPhase.FamilyRelationActions < YearPhase.Thoughts);
    }
}
