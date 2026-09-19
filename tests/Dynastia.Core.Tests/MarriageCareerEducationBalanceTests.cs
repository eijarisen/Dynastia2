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
    [InlineData(1, 0.05)]
    [InlineData(2, 0.15)]
    [InlineData(3, 0.30)]
    [InlineData(4, 0.50)]
    [InlineData(5, 0.70)]
    public void PaidEducationStronglyTracksIntellect(
        int intellect,
        double expectedChance)
    {
        Assert.Equal(
            expectedChance,
            EducationProgressionRules
                .GetPaidEducationSuccessChance(
                    intellect),
            6);
    }

    [Theory]
    [InlineData(1, 1, 0.25)]
    [InlineData(3, 3, 0.55)]
    [InlineData(5, 5, 0.85)]
    [InlineData(1, 5, 0.65)]
    [InlineData(5, 1, 0.45)]
    public void HelpInEducationUsesChildAndHelperIntellect(
        int childIntellect,
        int helperIntellect,
        double expectedChance)
    {
        Assert.Equal(
            expectedChance,
            EducationProgressionRules.GetHelpInEducationSuccessChance(
                childIntellect,
                helperIntellect),
            6);
    }

    [Fact]
    public void HigherLevelPromotionBlendsCareerAbilityWithIntellect()
    {
        Assert.Equal(
            5,
            CareerBalanceRules.GetPromotionAptitude(
                careerAbility: 5,
                intellect: 1,
                primaryIsIntellect: false,
                currentJobLevel: 2),
            6);

        Assert.Equal(
            3.4,
            CareerBalanceRules.GetPromotionAptitude(
                careerAbility: 5,
                intellect: 1,
                primaryIsIntellect: false,
                currentJobLevel: 3),
            6);

        Assert.Equal(
            5,
            CareerBalanceRules.GetPromotionAptitude(
                careerAbility: 5,
                intellect: 1,
                primaryIsIntellect: true,
                currentJobLevel: 4),
            6);

        Assert.Equal(0.35,
            CareerBalanceRules.GetEducationPromotionMultiplier(2, 3),
            6);
        Assert.Equal(0.10,
            CareerBalanceRules.GetEducationPromotionMultiplier(1, 3),
            6);
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
    [Fact]
    public void UpperCareerLevelsApplyAdditionalPromotionDifficulty()
    {
        Assert.Equal(1.0, CareerBalanceRules.GetTargetLevelPromotionMultiplier(3), 10);
        Assert.Equal(0.80, CareerBalanceRules.GetTargetLevelPromotionMultiplier(4), 10);
        Assert.Equal(0.35, CareerBalanceRules.GetTargetLevelPromotionMultiplier(5), 10);
    }

}
