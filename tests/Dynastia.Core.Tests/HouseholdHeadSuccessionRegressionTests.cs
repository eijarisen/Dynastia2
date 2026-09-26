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

    [Fact]
    public void HouseAtExactResidentCapacityShowsWarningBeforeOvercrowdingBegins()
    {
        using var fixture = new RefactorFixture();
        var head = fixture.Person(name: "Jan");
        fixture.Household(head);

        for (var index = 0; index < 5; index++)
        {
            var member = fixture.Person(name: $"Member{index}");
            member.Tags.Remove("family.bloodline");
            member.Tags.Remove("lineage.male");
            fixture.Economy.AddHouseholdMember(head, member);
        }

        var status = CreateService(fixture).GetStatus(head);

        Assert.NotNull(status);
        Assert.Equal(6, status.ResidentCount);
        Assert.Equal(6, status.OvercrowdingThreshold);
        Assert.False(status.IsOvercrowded);
        Assert.Contains(
            status.Warnings,
            warning => warning.Contains(
                "at its resident capacity",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RemarriedCaregiverKeepsStepfatherResidentAndAdultSonEstablishesOwnHousehold()
    {
        var guidCalls = Enumerable
            .Repeat(SequenceGameRandom.Int(0, 0xFFFF, 1), 8)
            .ToArray();
        using var fixture = new RefactorFixture(guidCalls);

        var mother = fixture.Person(age: 42, sex: Sex.Female, name: "Anna");
        mother.Tags.Remove("family.bloodline");
        mother.Tags.Remove("lineage.male");
        var lateFather = fixture.Person(age: 45, alive: false, name: "Piotr");
        var stepfather = fixture.Person(age: 44, name: "Tomasz");
        stepfather.Tags.Remove("family.bloodline");
        stepfather.Tags.Remove("lineage.male");
        var adultSon = fixture.Person(age: 18, name: "Michał");
        var youngerSon = fixture.Person(age: 12, name: "Jan");

        fixture.Family.SetParents(adultSon, lateFather, mother);
        fixture.Family.SetParents(youngerSon, lateFather, mother);
        fixture.Family.SetSpouses(mother, stepfather, fixture.State.Year);

        var caregiverHousehold = fixture.Household(mother, anchor: lateFather);
        fixture.Economy.AddHouseholdMember(mother, adultSon);
        fixture.Economy.AddHouseholdMember(mother, youngerSon);

        CreateService(fixture).ReconcileHouseholds();

        Assert.Equal(
            caregiverHousehold.HouseholdId,
            fixture.Economy.GetHouseholdId(mother));
        Assert.Equal(
            caregiverHousehold.HouseholdId,
            fixture.Economy.GetHouseholdId(stepfather));
        Assert.Equal(
            caregiverHousehold.HouseholdId,
            fixture.Economy.GetHouseholdId(youngerSon));

        Assert.True(fixture.Economy.HasHousehold(adultSon));
        Assert.NotEqual(
            caregiverHousehold.HouseholdId,
            fixture.Economy.GetHouseholdId(adultSon));
        Assert.True(adultSon.Tags.Has("residence.independent"));
    }

    [Fact]
    public void BereavementHealthIsHalvedAndStressIsDoubled()
    {
        Assert.Equal(7.5, FamilyShockRules.BereavementBaseHealthLoss, 10);
        Assert.Equal(2.0, FamilyShockRules.BereavementStressMultiplier, 10);
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
