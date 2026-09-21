using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Entities;
using Dynastia.Mechanics.Economy;
using Dynastia.Mechanics.FamilyRelations;
using Dynastia.Mechanics.Health;
using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed class Development10WorkCapacityRelationsHousingTests
{
    [Fact]
    public void SeriousConditionsReduceWorkCapacityAndIncapacitatingConditionsStopWork()
    {
        var health = new StandardHealthService(
            CreateRepositoryData(),
            new FixedRandom());
        var capacity = new StandardWorkCapacityService(health);

        var healthy = AlivePerson("Healthy", 40);
        health.EnsureHealth(healthy);
        Assert.Equal(1.0, capacity.GetWorkCapacity(healthy).OutputMultiplier, 6);

        var cold = AlivePerson("Cold", 40);
        health.EnsureHealth(cold);
        Assert.True(health.AddCondition(cold, "common_cold"));
        Assert.Equal(1.0, capacity.GetWorkCapacity(cold).OutputMultiplier, 6);

        var pneumonia = AlivePerson("Pneumonia", 40);
        health.EnsureHealth(pneumonia);
        Assert.True(health.AddCondition(pneumonia, "pneumonia"));
        var reduced = capacity.GetWorkCapacity(pneumonia);
        Assert.True(reduced.CanWork);
        Assert.Equal(0.50, reduced.OutputMultiplier, 6);

        var brokenLeg = AlivePerson("Broken Leg", 40);
        health.EnsureHealth(brokenLeg);
        Assert.True(health.AddCondition(brokenLeg, "broken_leg"));
        Assert.Equal(0.35, capacity.GetWorkCapacity(brokenLeg).OutputMultiplier, 6);

        var stroke = AlivePerson("Stroke", 65);
        health.EnsureHealth(stroke);
        Assert.True(health.AddCondition(stroke, "stroke"));
        var incapacitated = capacity.GetWorkCapacity(stroke);
        Assert.False(incapacitated.CanWork);
        Assert.Equal(0, incapacitated.OutputMultiplier);
    }

    [Fact]
    public void MissingPreReconciliationHealthDefaultsToFullWorkCapacity()
    {
        var health = new StandardHealthService(
            CreateRepositoryData(),
            new FixedRandom());
        var capacity = new StandardWorkCapacityService(health);
        var person = AlivePerson("Generated Adult", 24);

        Assert.False(person.Components.Has<HealthComponent>());

        var snapshot = capacity.GetWorkCapacity(person);

        Assert.True(snapshot.CanWork);
        Assert.Equal(1.0, snapshot.OutputMultiplier, 6);
        Assert.False(person.Components.Has<HealthComponent>());
    }

    [Fact]
    public void CriticalHealthStopsWorkButOtherwiseRawHealthDoesNotDoublePenalizeConditions()
    {
        var health = new StandardHealthService(
            CreateRepositoryData(),
            new FixedRandom());
        var capacity = new StandardWorkCapacityService(health);
        var person = AlivePerson("Weak", 40);
        health.SetHealth(person, 8);
        Assert.False(capacity.GetWorkCapacity(person).CanWork);

        health.SetHealth(person, 40);
        Assert.Equal(1.0, capacity.GetWorkCapacity(person).OutputMultiplier, 6);
        Assert.True(health.AddCondition(person, "pneumonia"));
        var before = health.GetHealth(person).Current;
        var snapshot = capacity.GetWorkCapacity(person);

        Assert.Equal(0.50, snapshot.OutputMultiplier, 6);
        Assert.Equal(before, health.GetHealth(person).Current, 6);
    }

    [Fact]
    public void MultipleConditionsUseTheSingleMostRestrictiveWorkMultiplier()
    {
        var health = new StandardHealthService(
            CreateRepositoryData(),
            new FixedRandom());
        var capacity = new StandardWorkCapacityService(health);
        var person = AlivePerson("Injured", 40);
        health.EnsureHealth(person);

        Assert.True(health.AddCondition(person, "broken_arm"));
        Assert.True(health.AddCondition(person, "broken_leg"));

        Assert.Equal(0.35, capacity.GetWorkCapacity(person).OutputMultiplier, 6);
    }

    [Fact]
    public void WeddingGiftsProtectPoorHouseholdsAndScaleWithDisposableWealth()
    {
        Assert.Equal(
            0m,
            WeddingSupportRules.CalculateGift(
                1_900m,
                2_000m,
                90,
                80));

        var modest = WeddingSupportRules.CalculateGift(
            12_000m,
            2_000m,
            80,
            65);
        var rich = WeddingSupportRules.CalculateGift(
            120_000m,
            5_000m,
            90,
            85);

        Assert.True(modest >= 100m);
        Assert.True(rich > modest);
        Assert.True(rich <= 2_500m);

        Assert.Equal(
            0m,
            WeddingSupportRules.CalculateGift(
                120_000m,
                5_000m,
                50,
                50));
    }

    [Fact]
    public void GreaterDistanceProducesStrongerNeutralizingDriftWithoutHostility()
    {
        var nearby = FamilyRelationDistanceRules.GetAnnualDrift(20, false);
        var distant = FamilyRelationDistanceRules.GetAnnualDrift(450, false);
        var coResident = FamilyRelationDistanceRules.GetAnnualDrift(450, true);

        Assert.Equal(0.10, nearby.FamiliarityLoss, 6);
        Assert.Equal(0.15, nearby.SympathyDrift, 6);
        Assert.Equal(0.65, distant.FamiliarityLoss, 6);
        Assert.Equal(0.85, distant.SympathyDrift, 6);
        Assert.Equal(0, coResident.FamiliarityLoss);
        Assert.Equal(0, coResident.SympathyDrift);

        var sameTown = FamilyRelationDistanceRules.GetAnnualDrift(0.5, false);
        Assert.Equal(0, sameTown.FamiliarityLoss);
        Assert.Equal(0, sameTown.SympathyDrift);

        Assert.True(
            FamilyRelationDistanceRules.DistanceKm(
                52.2297,
                21.0122,
                50.0647,
                19.9450) > 200);
    }

    [Fact]
    public void HouseExtensionsCostQuarterOfPurchasePriceAndAddTwoCapacity()
    {
        Assert.Equal(10_000m, HouseExtensionRules.GetExtensionCost(40_000m));
        Assert.Equal(6, HouseExtensionRules.GetResidentCapacity(0));
        Assert.Equal(8, HouseExtensionRules.GetResidentCapacity(1));
        Assert.Equal(12, HouseExtensionRules.GetResidentCapacity(3));

        Assert.Equal(
            0,
            HouseholdCrowdingRules.GetResidentsAboveCapacity(10, 10));
        Assert.Equal(
            2,
            HouseholdCrowdingRules.GetResidentsAboveCapacity(12, 10));
    }

    private static Person AlivePerson(string name, int age)
    {
        var person = new Person(name, "Test", age);
        person.Tags.Add("state.alive");
        return person;
    }

    private static IGameDataService CreateRepositoryData()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var dataPath = Path.Combine(directory.FullName, "data");
            if (File.Exists(Path.Combine(dataPath, "Common", "health_conditions.json")))
                return new JsonGameDataService(dataPath);
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository data directory from test output.");
    }

    private sealed class FixedRandom : IGameRandom
    {
        public int NextInt(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => 0.5;
        public bool Chance(double probability) => probability >= 0.5;
    }
}
