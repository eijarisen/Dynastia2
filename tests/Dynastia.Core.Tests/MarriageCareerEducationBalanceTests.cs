using Dynastia.Contracts;
using Dynastia.Mechanics.Career;
using Dynastia.Mechanics.Education;
using Dynastia.Mechanics.Relationships;

namespace Dynastia.Core.Tests;

public sealed class MarriageCareerEducationBalanceTests
{
    [Fact]
    public void ContentMarriageCannotAutomaticallyCollapse()
    {
        Assert.Equal(0, MarriageBalanceRules.GetAutomaticDivorceChance(40));
        Assert.Equal(0, MarriageBalanceRules.GetAutomaticDivorceChance(65));
    }

    [Fact]
    public void EmploymentBuffersOrdinaryFinancialPressureInMarriage()
    {
        var employedPenalty =
            MarriageBalanceRules.GetFinancialPressurePenalty(
                isBroke: true,
                hasWorkingSpouse: true);

        var unemployedPenalty =
            MarriageBalanceRules.GetFinancialPressurePenalty(
                isBroke: true,
                hasWorkingSpouse: false);

        Assert.True(
            MarriageBalanceRules.GetAnnualSatisfactionChange(employedPenalty) > 0);

        Assert.True(
            MarriageBalanceRules.GetAnnualSatisfactionChange(unemployedPenalty) < 0);
    }

    [Fact]
    public void UnhappyMarriageHasOnlySmallAutomaticDivorceChance()
    {
        Assert.Equal(0.015, MarriageBalanceRules.GetAutomaticDivorceChance(25), 6);
        Assert.Equal(0.005, MarriageBalanceRules.GetAutomaticDivorceChance(35), 6);
        Assert.True(MarriageBalanceRules.GetAutomaticDivorceChance(15) >
                    MarriageBalanceRules.GetAutomaticDivorceChance(25));
    }

    [Theory]
    [InlineData(1, CareerOpportunityStrength.None, 0.35)]
    [InlineData(3, CareerOpportunityStrength.None, 0.65)]
    [InlineData(5, CareerOpportunityStrength.None, 0.90)]
    [InlineData(5, CareerOpportunityStrength.Town, 0.95)]
    public void EmploymentSearchRewardsAptitudeAndLocalOpportunity(
        int aptitude,
        CareerOpportunityStrength strength,
        double expected)
    {
        Assert.Equal(
            expected,
            CareerBalanceRules.GetEmploymentSearchChance(aptitude, strength),
            6);
    }

    [Fact]
    public void WorkHarderBenefitsFromEducation()
    {
        var poorlyEducated =
            CareerBalanceRules.GetWorkHarderPromotionBonus(3, 1);

        var highlyEducated =
            CareerBalanceRules.GetWorkHarderPromotionBonus(3, 5);

        Assert.True(highlyEducated > poorlyEducated);
        Assert.Equal(0.44, highlyEducated, 6);
    }

    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(2, 2, 3)]
    [InlineData(3, 3, 4)]
    [InlineData(4, 4, 5)]
    [InlineData(5, 5, 5)]
    public void ChildhoodEducationTracksIntellectButParentalHelpCanBoostIt(
        int intellect,
        int passiveCeiling,
        int helpedCeiling)
    {
        Assert.Equal(
            passiveCeiling,
            EducationProgressionRules.GetPassiveChildhoodCeiling(intellect));

        Assert.Equal(
            helpedCeiling,
            EducationProgressionRules.GetHelpedChildhoodCeiling(intellect));
    }
}
