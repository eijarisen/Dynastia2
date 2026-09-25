using System.Reflection;
using Dynastia.Contracts;
using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed class HouseholdHeadSuccessionRegressionTests
{
    [Fact]
    public void StaleResidentWhoAlreadyHeadsAnotherHouseholdIsNotUsedAsSuccessor()
    {
        using var fixture = new RefactorFixture();
        var caregiver = fixture.Person(age: 55, sex: Sex.Female, name: "Anna");
        caregiver.Tags.Remove("family.bloodline");
        caregiver.Tags.Remove("lineage.male");

        var adultSon = fixture.Person(age: 30, sex: Sex.Male, name: "Michał");
        var caregiverHousehold = fixture.Household(caregiver);
        var sonHousehold = fixture.Household(adultSon);

        // Reproduce the inconsistent state behind the reported crash: the son
        // already owns a household component but is still present in the old
        // household's member list.
        caregiverHousehold.MemberIds.Add(adultSon.Id);

        var service = CreateService(fixture);

        var exception = Record.Exception(service.ReconcileHouseholds);

        Assert.Null(exception);
        Assert.Equal(
            caregiverHousehold.HouseholdId,
            fixture.Economy.GetHouseholdId(caregiver));
        Assert.Equal(
            sonHousehold.HouseholdId,
            fixture.Economy.GetHouseholdId(adultSon));
    }

    [Fact]
    public void EconomyReconciliationRemovesForeignHouseholdHeadsFromMemberLists()
    {
        using var fixture = new RefactorFixture();
        var firstHead = fixture.Person(name: "First");
        var secondHead = fixture.Person(name: "Second");
        var firstHousehold = fixture.Household(firstHead);
        var secondHousehold = fixture.Household(secondHead);
        firstHousehold.MemberIds.Add(secondHead.Id);

        fixture.Economy.ReconcileState();

        Assert.DoesNotContain(secondHead.Id, firstHousehold.MemberIds);
        Assert.Contains(secondHead.Id, secondHousehold.MemberIds);
        Assert.Equal(secondHousehold.HouseholdId, fixture.Economy.GetHouseholdId(secondHead));
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
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args) =>
            throw new NotSupportedException(
                $"Unexpected test dependency call: {targetMethod?.Name}.");
    }
}
