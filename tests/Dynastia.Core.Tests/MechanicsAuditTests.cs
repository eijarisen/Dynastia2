using Dynastia.Core.Entities;
using Dynastia.Mechanics.Career;
using Dynastia.Mechanics.Health;
using Dynastia.Mechanics.Relationships;

namespace Dynastia.Core.Tests;

public sealed class MechanicsAuditTests
{
    [Fact]
    public void StartingLevelThreeCareerIsRareAndRequiresEducation()
    {
        Assert.NotEqual(
            3,
            InitialCareerProfileRules.ResolveJobLevel(
                age: 45,
                educationLevel: 0,
                roll: 0.999));

        Assert.Equal(
            2,
            InitialCareerProfileRules.ResolveJobLevel(
                age: 45,
                educationLevel: 2,
                roll: 0.93));

        Assert.Equal(
            3,
            InitialCareerProfileRules.ResolveJobLevel(
                age: 45,
                educationLevel: 2,
                roll: 0.97));

        Assert.Equal(
            2,
            InitialCareerProfileRules.ResolveJobLevel(
                age: 22,
                educationLevel: 3,
                roll: 0.95));

        Assert.Equal(
            3,
            InitialCareerProfileRules.ResolveJobLevel(
                age: 22,
                educationLevel: 3,
                roll: 0.97));
    }

    [Fact]
    public void SevereStressCanProduceAlcoholismOnlyForAdults()
    {
        var adult = new Person("Jan", "Test", 30);
        adult.Tags.Add("personality.choleric");
        adult.Tags.Add("morals.evil");

        var child = new Person("Adam", "Test", 16);
        child.Tags.Add("personality.choleric");
        child.Tags.Add("morals.evil");

        Assert.Equal(
            0.0,
            MentalHealthStressRules.GetAlcoholismWeight(
                adult,
                stress: 30));

        Assert.True(
            MentalHealthStressRules.GetAlcoholismWeight(
                adult,
                stress: 60) > 0);

        Assert.Equal(
            0.0,
            MentalHealthStressRules.GetAlcoholismWeight(
                child,
                stress: 80));
    }

    [Fact]
    public void ExistingStressConditionsReduceButDoNotDisableFurtherStressReaction()
    {
        var person = new Person("Anna", "Test", 30);
        person.Tags.Add("personality.melancholic");

        var first = MentalHealthStressRules.GetReactionChance(
            stress: 60,
            person,
            existingStressConditions: 0);

        var second = MentalHealthStressRules.GetReactionChance(
            stress: 60,
            person,
            existingStressConditions: 1);

        var third = MentalHealthStressRules.GetReactionChance(
            stress: 60,
            person,
            existingStressConditions: 2);

        Assert.True(first > second);
        Assert.True(second > third);
        Assert.True(third > 0);
    }

    [Fact]
    public void MarriageSatisfactionStillHasMeaningfulPressureAndDivorceEffects()
    {
        Assert.Equal(
            1.0,
            MarriageBalanceRules.GetAnnualSatisfactionChange(0),
            6);

        Assert.True(
            MarriageBalanceRules.GetAnnualSatisfactionChange(8) < 0);

        Assert.Equal(
            0,
            MarriageBalanceRules.GetAutomaticDivorceChance(40),
            6);

        Assert.True(
            MarriageBalanceRules.GetAutomaticDivorceChance(15)
            > MarriageBalanceRules.GetAutomaticDivorceChance(35));

        Assert.True(
            MarriageBalanceRules.GetAnnualSatisfactionChange(
                MarriageBalanceRules.UnemployedHusbandPenalty) < 0);

        Assert.True(
            MarriageBalanceRules.GetAnnualSatisfactionChange(
                MarriageBalanceRules.ImprisonmentPenalty)
            < MarriageBalanceRules.GetAnnualSatisfactionChange(
                MarriageBalanceRules.UnemployedHusbandPenalty));
    }
}
