using Dynastia.Mechanics.Households;
using Dynastia.Mechanics.Reproduction;

namespace Dynastia.Core.Tests;

public sealed class AutonomousStrategyRulesTests
{
    [Theory]
    [InlineData(-100, 1000, 900, AutonomousFinancialState.Critical)]
    [InlineData(0, 1200, 1000, AutonomousFinancialState.Poor)]
    [InlineData(1500, 1200, 1000, AutonomousFinancialState.Stable)]
    [InlineData(4000, 1400, 1000, AutonomousFinancialState.Secure)]
    public void FinancialAssessmentUsesSurvivalFirstBands(
        int wealth,
        int income,
        int expenses,
        AutonomousFinancialState expected)
    {
        Assert.Equal(
            expected,
            AutonomousStrategyRules.GetFinancialState(
                wealth,
                income,
                expenses));
    }

    [Fact]
    public void ActiveChildbirthRequiresStableUnstrainedHousehold()
    {
        Assert.True(
            AutonomousStrategyRules.CanActivelyTryForChild(
                livingChildren: 0,
                AutonomousFinancialState.Stable,
                strained: false,
                dependentChildren: 0,
                effectiveCapacity: 3,
                hasReproductivePath: true));

        Assert.False(
            AutonomousStrategyRules.CanActivelyTryForChild(
                livingChildren: 0,
                AutonomousFinancialState.Poor,
                strained: false,
                dependentChildren: 0,
                effectiveCapacity: 3,
                hasReproductivePath: true));

        Assert.False(
            AutonomousStrategyRules.CanActivelyTryForChild(
                livingChildren: 1,
                AutonomousFinancialState.Stable,
                strained: true,
                dependentChildren: 1,
                effectiveCapacity: 3,
                hasReproductivePath: true));

        Assert.False(
            AutonomousStrategyRules.CanActivelyTryForChild(
                livingChildren: 2,
                AutonomousFinancialState.Secure,
                strained: false,
                dependentChildren: 2,
                effectiveCapacity: 4,
                hasReproductivePath: true));
    }

    [Theory]
    [InlineData(70, true, false, false, false)]
    [InlineData(60, false, false, false, true)]
    [InlineData(70, false, false, false, false)]
    [InlineData(94, false, true, false, true)]
    [InlineData(30, false, false, true, false)]
    public void ReproductiveSpouseSearchHonoursMoralAgeGapRules(
        int age,
        bool good,
        bool evil,
        bool homosexual,
        bool expected)
    {
        Assert.Equal(
            expected,
            AutonomousStrategyRules.CanSearchForReproductiveFemale(
                age,
                good,
                evil,
                homosexual));
    }

    [Fact]
    public void RandomCompetitionIsLimitedToActionsWithinFifteenPercent()
    {
        Assert.True(
            AutonomousStrategyRules.IsCloseEnoughToCompete(85, 100));
        Assert.False(
            AutonomousStrategyRules.IsCloseEnoughToCompete(84.9, 100));
    }

    [Fact]
    public void FailedActiveConceptionAttemptCostsExactlyTwoMarriageSatisfaction()
    {
        Assert.Equal(
            -2.0,
            ReproductionBalanceRules.GetMarriageSatisfactionChange(
                activelyTried: true,
                conceived: false));

        Assert.Equal(
            0.0,
            ReproductionBalanceRules.GetMarriageSatisfactionChange(
                activelyTried: false,
                conceived: false));

        Assert.Equal(
            0.0,
            ReproductionBalanceRules.GetMarriageSatisfactionChange(
                activelyTried: true,
                conceived: true));
    }
}
