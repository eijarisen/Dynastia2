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
    [InlineData(1, 0, 0)]
    [InlineData(2, 1, 2)]
    [InlineData(3, 1, 2)]
    [InlineData(4, 2, 3)]
    [InlineData(5, 3, 4)]
    public void CareerConnectionsPlaceBelowTheStrongestCurrentJob(
        int strongestJobLevel,
        int expectedStandard,
        int expectedExceptional)
    {
        Assert.Equal(
            expectedStandard,
            FamilyCareerConnectionRules.GetStandardPlacementLevel(strongestJobLevel));
        Assert.Equal(
            expectedExceptional,
            FamilyCareerConnectionRules.GetExceptionalPlacementLevel(strongestJobLevel));
    }

    [Fact]
    public void CareerConnectionsCannotBootstrapAboveTheirSponsor()
    {
        Assert.False(FamilyCareerConnectionRules.CanProvideHelp(1));
        Assert.True(FamilyCareerConnectionRules.CanProvideHelp(2));

        Assert.True(FamilyCareerConnectionRules.CanImprove(0, 3));
        Assert.True(FamilyCareerConnectionRules.CanImprove(1, 3));
        Assert.False(FamilyCareerConnectionRules.CanImprove(2, 3));
        Assert.False(FamilyCareerConnectionRules.CanImprove(3, 3));
    }

    [Fact]
    public void RelationsActionsRunBeforeThoughtGeneration()
    {
        Assert.True(YearPhase.MoralsReflection < YearPhase.FamilyRelations);
        Assert.True(YearPhase.FamilyRelations < YearPhase.FamilyRelationActions);
        Assert.True(YearPhase.FamilyRelationActions < YearPhase.Thoughts);
    }
}
