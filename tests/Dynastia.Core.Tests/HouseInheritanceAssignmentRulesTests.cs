using Dynastia.Mechanics.Inheritance;

namespace Dynastia.Core.Tests;

public sealed class HouseInheritanceAssignmentRulesTests
{
    [Fact]
    public void DesignatedHousePrecedesStandardInheritance()
    {
        var eldest = Guid.NewGuid();
        var younger = Guid.NewGuid();

        var recipients =
            HouseInheritanceAssignmentRules.ResolveRecipients(
                [eldest, younger],
                [younger, null, null]);

        Assert.Equal(
            new[] { younger, eldest, younger },
            recipients);
    }

    [Fact]
    public void InvalidDesignationFallsBackToStandardInheritance()
    {
        var eldest = Guid.NewGuid();
        var younger = Guid.NewGuid();
        var deceasedChild = Guid.NewGuid();

        var recipients =
            HouseInheritanceAssignmentRules.ResolveRecipients(
                [eldest, younger],
                [deceasedChild, null, null]);

        Assert.Equal(
            new[] { eldest, younger, eldest },
            recipients);
    }

    [Fact]
    public void NoDesignationsPreserveExistingRoundRobinRule()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        var recipients =
            HouseInheritanceAssignmentRules.ResolveRecipients(
                [first, second, third],
                [null, null, null, null]);

        Assert.Equal(
            new[] { first, second, third, first },
            recipients);
    }
}
