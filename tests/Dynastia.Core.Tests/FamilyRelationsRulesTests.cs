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

    [Fact]
    public void AbilityModifiesButDoesNotReplaceRelationshipWillingness()
    {
        var barelyAble = FamilyRelationScoreRules.GetRequestWillingness(70, 0.75);
        var comfortable = FamilyRelationScoreRules.GetRequestWillingness(70, 1.10);

        Assert.True(barelyAble < comfortable);
        Assert.InRange(comfortable, 0, 0.95);
    }

    [Fact]
    public void RelationsActionsRunBeforeThoughtGeneration()
    {
        Assert.True(YearPhase.MoralsReflection < YearPhase.FamilyRelations);
        Assert.True(YearPhase.FamilyRelations < YearPhase.FamilyRelationActions);
        Assert.True(YearPhase.FamilyRelationActions < YearPhase.Thoughts);
    }
}
