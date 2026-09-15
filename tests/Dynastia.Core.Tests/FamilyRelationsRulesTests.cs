using Dynastia.Contracts;
using Dynastia.Mechanics.FamilyRelations;

namespace Dynastia.Core.Tests;

public sealed class FamilyRelationsRulesTests
{
    [Theory]
    [InlineData(0, "Hostile")]
    [InlineData(19, "Hostile")]
    [InlineData(20, "Poor")]
    [InlineData(40, "Neutral")]
    [InlineData(60, "Good")]
    [InlineData(80, "Close")]
    [InlineData(100, "Close")]
    public void RelationshipLabelsFollowSpecification(double score, string expected)
    {
        Assert.Equal(expected, FamilyRelationScoreRules.GetDisplayState(score));
    }

    [Fact]
    public void RelationshipIsThePrimarySocialInputToRequestWillingness()
    {
        var hostile = FamilyRelationScoreRules.GetRequestWillingness(10, 1.0);
        var neutral = FamilyRelationScoreRules.GetRequestWillingness(50, 1.0);
        var close = FamilyRelationScoreRules.GetRequestWillingness(90, 1.0);

        Assert.True(hostile < neutral);
        Assert.True(neutral < close);
    }


    [Theory]
    [InlineData(10, 0.10)]
    [InlineData(30, 0.25)]
    [InlineData(50, 0.50)]
    [InlineData(70, 0.72)]
    [InlineData(90, 0.95)]
    public void RequestWillingnessTracksRelationshipBand(double score, double expected)
    {
        Assert.Equal(expected, FamilyRelationScoreRules.GetRequestWillingness(score), 6);
    }

    [Fact]
    public void AbilityModifiesButDoesNotReplaceRelationshipWillingness()
    {
        var barelyAble = FamilyRelationScoreRules.GetRequestWillingness(70, 0.75);
        var comfortable = FamilyRelationScoreRules.GetRequestWillingness(70, 1.10);

        Assert.True(barelyAble < comfortable);
        Assert.InRange(comfortable, 0, 0.95);
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
