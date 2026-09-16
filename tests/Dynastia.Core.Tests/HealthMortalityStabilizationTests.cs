using Dynastia.Contracts;
using Dynastia.Core.Entities;
using Dynastia.Mechanics.Health;
using Dynastia.Mechanics.Mortality;
using Dynastia.Mechanics.Reproduction;

namespace Dynastia.Core.Tests;

public sealed class HealthMortalityStabilizationTests
{
    [Fact]
    public void NormalHealthChangesClampToValidRange()
    {
        var health = new StandardHealthService(
            new MinimalHealthData(),
            new FixedRandom());
        var person = new Person("Jan", "Test", 30);

        health.SetHealth(person, -25);
        Assert.Equal(0, health.GetHealth(person).Current);

        health.SetHealth(person, 150);
        Assert.Equal(100, health.GetHealth(person).Current);

        health.ChangeHealth(person, -250);
        Assert.Equal(0, health.GetHealth(person).Current);
    }

    [Fact]
    public void LegacyNegativeHealthIsNormalizedOnRead()
    {
        var health = new StandardHealthService(
            new MinimalHealthData(),
            new FixedRandom());
        var person = new Person("Anna", "Test", 30);
        person.Components.Set(
            new HealthComponent
            {
                Current = -18,
                Maximum = 100
            });

        Assert.Equal(0, health.GetHealth(person).Current);
    }

    [Fact]
    public void PhysicalDiseaseIncidenceUsesGlobalReduction()
    {
        Assert.Equal(
            0.126,
            HealthIncidenceRules.ScaleMildConditionChance(0.28),
            6);
        Assert.Equal(
            0.0035,
            HealthIncidenceRules.ScaleSeriousConditionChance(0.01),
            6);
    }


    [Fact]
    public void HealthSeverityIncreasesImpactWithoutChangingIncidence()
    {
        var mild = new HealthConditionDefinition
        {
            Id = "mild",
            Name = "Mild",
            Type = "seasonal",
            Category = "Mild",
            Course = "Acute",
            HealthImpact = -10
        };
        var serious = new HealthConditionDefinition
        {
            Id = "serious",
            Name = "Serious",
            Type = "curable",
            Category = "Serious",
            Course = "Acute",
            HealthImpact = -10,
            ImmediateHealthImpact = -40
        };

        Assert.Equal(-13.5, HealthSeverityRules.ScaleAnnualImpact(mild), 6);
        Assert.Equal(-14.0, HealthSeverityRules.ScaleAnnualImpact(serious), 6);
        Assert.Equal(-50.0, HealthSeverityRules.ScaleImmediateImpact(serious), 6);

        Assert.Equal(
            0.126,
            HealthIncidenceRules.ScaleMildConditionChance(0.28),
            6);
    }

    [Fact]
    public void LoadedConditionsAdoptCurrentSeverityTuning()
    {
        var health = new StandardHealthService(
            new MinimalHealthData(),
            new FixedRandom());
        var person = new Person("Anna", "Test", 30);
        person.Components.Set(
            new HealthComponent
            {
                Current = 100,
                Maximum = 100,
                Conditions =
                {
                    new HealthConditionState
                    {
                        Id = "test_condition",
                        Name = "Test Condition",
                        Type = "seasonal",
                        HealthImpact = -1,
                        RemainingYears = 1
                    }
                }
            });

        var condition = health.GetHealth(person).Conditions.Single();

        Assert.Equal(-1.35, condition.HealthImpact, 6);
    }

    [Fact]
    public void RandomMortalityIsReducedButZeroHealthIsNotPartOfScale()
    {
        Assert.Equal(0.04, MortalityRules.ScaleRandomMortality(0.10), 6);
        Assert.Equal(0.0008, MortalityRules.GenericAccidentChance, 7);
    }

    [Theory]
    [InlineData(1, 52)]
    [InlineData(2, 64)]
    [InlineData(3, 76)]
    [InlineData(4, 88)]
    [InlineData(5, 100)]
    public void NaturalDeathCurveIsCenteredOnLongevityProfile(
        int longevity,
        int profileAge)
    {
        Assert.Equal(
            profileAge,
            MortalityRules.GetLongevityProfileAge(longevity));

        Assert.Equal(
            MortalityRules.NaturalDeathChanceAtProfileAge,
            MortalityRules.GetNaturalDeathChance(
                profileAge,
                longevity,
                immunity: 3),
            6);
    }

    [Fact]
    public void LongevityDominatesVeryOldNaturalMortality()
    {
        var ordinary =
            MortalityRules.GetNaturalDeathChance(
                age: 100,
                longevity: 3,
                immunity: 5);

        var strong =
            MortalityRules.GetNaturalDeathChance(
                age: 100,
                longevity: 4,
                immunity: 5);

        var exceptional =
            MortalityRules.GetNaturalDeathChance(
                age: 100,
                longevity: 5,
                immunity: 5);

        Assert.Equal(
            MortalityRules.MaximumNaturalDeathChance,
            ordinary,
            6);

        Assert.True(strong > 0.30);
        Assert.True(exceptional < 0.08);
        Assert.True(strong > exceptional * 4);
    }

    [Theory]
    [InlineData(1, 1.20)]
    [InlineData(2, 1.10)]
    [InlineData(3, 1.00)]
    [InlineData(4, 0.90)]
    [InlineData(5, 0.80)]
    public void BirthConditionLongevityModifierIsModest(
        int longevity,
        double expectedModifier)
    {
        Assert.Equal(
            expectedModifier,
            BirthConditionRules.GetLongevityModifier(longevity),
            6);
    }

    [Fact]
    public void BirthConditionSelectionReturnsAtMostOneCondition()
    {
        var definitions = new List<BirthConditionDefinition>
        {
            new() { Id = "first", Name = "First", Probability = 0.03 },
            new() { Id = "second", Name = "Second", Probability = 0.02 }
        };

        var selected = BirthConditionRules.SelectCondition(
            definitions,
            roll: 0.015,
            longevity: 3);

        Assert.NotNull(selected);
        Assert.Contains(selected!.Id, new[] { "first", "second" });
    }

    [Fact]
    public void BirthConditionTotalUsesGlobalReduction()
    {
        const double currentTableTotal = 0.0497;

        var longevityThree =
            currentTableTotal * BirthConditionRules.GetProbabilityScale(3);
        var longevityOne =
            currentTableTotal * BirthConditionRules.GetProbabilityScale(1);

        Assert.Equal(0.01988, longevityThree, 6);
        Assert.Equal(0.023856, longevityOne, 6);
        Assert.True(longevityOne < currentTableTotal);
    }

    private sealed class MinimalHealthData : IGameDataService
    {
        public IReadOnlyList<string> GetStringList(string relativePath) => [];

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(
            string relativePath) => [];

        public string ReadText(string relativePath) =>
            """
            [
              {
                "id": "test_condition",
                "name": "Test Condition",
                "type": "seasonal",
                "category": "Mild",
                "course": "Acute",
                "minimumAge": 0,
                "healthImpact": -1,
                "weight": 1,
                "durationMin": 1,
                "durationMax": 1
              }
            ]
            """;
    }

    private sealed class FixedRandom : IGameRandom
    {
        public int NextInt(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => 0.5;
        public bool Chance(double probability) => probability >= 0.5;
    }
}
