using Dynastia.Mechanics.Relationships;

namespace Dynastia.Core.Tests;

public sealed class DivorceCustodyRulesTests
{
    [Fact]
    public void OnlyExactSharedBiologicalChildrenQualifyForCustody()
    {
        var father = Guid.NewGuid();
        var mother = Guid.NewGuid();
        var previousPartner = Guid.NewGuid();

        Assert.True(
            DivorceCustodyRules.IsSharedBiologicalChild(
                father,
                mother,
                father,
                mother));

        Assert.True(
            DivorceCustodyRules.IsSharedBiologicalChild(
                father,
                mother,
                mother,
                father));

        Assert.False(
            DivorceCustodyRules.IsSharedBiologicalChild(
                father,
                previousPartner,
                father,
                mother));
    }

    [Fact]
    public void IndependentCustodyRollsCanSplitSiblings()
    {
        Assert.True(DivorceCustodyRules.AssignToFather(0.10));
        Assert.False(DivorceCustodyRules.AssignToFather(0.90));
    }
}
