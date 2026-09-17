using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed class AutonomousWorkChoiceRulesTests
{
    [Fact]
    public void UrgentHouseholdValuesJobByImmediateSuccessChance()
    {
        Assert.Equal(600m, AutonomousWorkChoiceRules.GetCareerOpportunityValue(1000m, 0.60, urgent: true));
    }

    [Fact]
    public void StableHouseholdCreditsPersistentCareerValueButStillRespectsChance()
    {
        Assert.Equal(820m, AutonomousWorkChoiceRules.GetCareerOpportunityValue(1000m, 0.60, urgent: false));
    }

    [Fact]
    public void WorkPathRequiresMeaningfulIncomeImprovementBeforeSwitching()
    {
        Assert.False(AutonomousWorkChoiceRules.IsMateriallyBetter(1040m, 1000m));
        Assert.True(AutonomousWorkChoiceRules.IsMateriallyBetter(1050m, 1000m));
    }
}
