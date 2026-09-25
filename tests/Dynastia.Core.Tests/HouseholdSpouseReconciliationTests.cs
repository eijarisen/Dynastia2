using System.Reflection;
using Dynastia.Contracts;
using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed class HouseholdSpouseReconciliationTests
{
    [Fact]
    public void CurrentSpouseWhoHeadsAnotherHouseholdIsNotAbsorbed()
    {
        using var fixture = new RefactorFixture();
        var bloodline = fixture.Person(name: "Jan");
        var spouse = fixture.Person(sex: Sex.Female, name: "Matilda");
        spouse.Tags.Remove("family.bloodline");
        fixture.Family.SetSpouses(bloodline, spouse, fixture.State.Year);

        var bloodlineHousehold = fixture.Household(bloodline);
        var spouseHousehold = fixture.Household(spouse);

        var service = CreateService(fixture);

        service.ReconcileHouseholds();

        Assert.True(fixture.Economy.HasHousehold(bloodline));
        Assert.True(fixture.Economy.HasHousehold(spouse));
        Assert.Equal(
            bloodlineHousehold.HouseholdId,
            fixture.Economy.GetHouseholdId(bloodline));
        Assert.Equal(
            spouseHousehold.HouseholdId,
            fixture.Economy.GetHouseholdId(spouse));
        Assert.DoesNotContain(
            spouse.Id,
            fixture.Economy.GetHouseholdMemberIds(bloodline));
    }

    [Fact]
    public void CurrentSpouseWhoIsOnlyAResidentStillMovesToBloodlineHousehold()
    {
        using var fixture = new RefactorFixture();
        var bloodline = fixture.Person(name: "Jan");
        var spouse = fixture.Person(sex: Sex.Female, name: "Anna");
        spouse.Tags.Remove("family.bloodline");
        var formerHead = fixture.Person(name: "FormerHead");
        spouse.Tags.Remove("lineage.male");
        fixture.Family.SetSpouses(bloodline, spouse, fixture.State.Year);

        var target = fixture.Household(bloodline);
        fixture.Household(formerHead);
        fixture.Economy.AddHouseholdMember(formerHead, spouse);

        var service = CreateService(fixture);

        service.ReconcileHouseholds();

        Assert.False(fixture.Economy.HasHousehold(spouse));
        Assert.Equal(
            target.HouseholdId,
            fixture.Economy.GetHouseholdId(spouse));
        Assert.Contains(
            spouse.Id,
            fixture.Economy.GetHouseholdMemberIds(bloodline));
        Assert.DoesNotContain(
            spouse.Id,
            fixture.Economy.GetHouseholdMemberIds(formerHead));
    }

    private static StandardHouseholdService CreateService(
        RefactorFixture fixture) =>
        new(
            fixture.State,
            fixture.Family,
            fixture.Economy,
            fixture.Economy,
            Unused<ILocationService>(),
            Unused<ICareerService>(),
            Unused<IFarmingService>(),
            fixture.Events);

    private static T Unused<T>()
        where T : class =>
        DispatchProxy.Create<T, UnusedDispatchProxy>();

    public class UnusedDispatchProxy : DispatchProxy
    {
        public UnusedDispatchProxy()
        {
        }

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args) =>
            throw new NotSupportedException(
                $"Unexpected test dependency call: {targetMethod?.Name}.");
    }
}
