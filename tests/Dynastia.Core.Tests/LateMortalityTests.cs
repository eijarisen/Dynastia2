using System.Reflection;
using Dynastia.Contracts;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Health;
using Dynastia.Mechanics.Mortality;

namespace Dynastia.Core.Tests;

public sealed class LateMortalityTests
{
    [Fact]
    public void ZeroHealthCreatedDuringLifeEventsDiesBeforeYearCompletes()
    {
        var gameState = new GameState();
        var person = gameState.CreatePerson("Jan", "Test", 35);
        person.Tags.Add("state.alive");

        var health = new StandardHealthService(
            new MinimalHealthData(),
            new FixedRandom());
        health.EnsureHealth(person);

        var family = new MinimalFamilyService();
        var deaths = new MortalityDeathService(
            new FixedStatsService(),
            health,
            family,
            DispatchProxy.Create<IEconomyService, NullDispatchProxy>(),
            new FixedRandom(),
            new GameCalendar(),
            new GameEventBus());

        var registry = new YearSystemRegistry();
        registry.Register(
            new LifeEventDamageSystem(
                person,
                health));
        registry.Register(
            new ZeroHealthResolutionYearSystem(
                health,
                deaths));

        new YearProcessor(
            gameState,
            registry)
            .AdvanceYear();

        Assert.True(person.Tags.Has("state.dead"));
        Assert.False(person.Tags.Has("state.alive"));
        Assert.Equal(0, health.GetHealth(person).Current);
        Assert.DoesNotContain(
            gameState.People,
            candidate =>
                candidate.Tags.Has("state.alive")
                && health.GetHealth(candidate).Current <= 0);
    }

    private sealed class LifeEventDamageSystem : IYearSystem
    {
        private readonly IPerson _person;
        private readonly IHealthService _health;

        public LifeEventDamageSystem(
            IPerson person,
            IHealthService health)
        {
            _person = person;
            _health = health;
        }

        public string Id => "test.life_event_damage";
        public YearPhase Phase => YearPhase.LifeEvents;
        public IReadOnlyCollection<string> Before => [];
        public IReadOnlyCollection<string> After => [];

        public void Execute(IGameState gameState) =>
            _health.SetHealth(_person, 0);
    }

    private sealed class FixedStatsService : IStatsService
    {
        private static readonly IReadOnlyList<StatValue> Values =
        [
            new StatValue("longevity", "Longevity", 3, string.Empty)
        ];

        public IReadOnlyList<StatValue> GetStats(IPerson person) => Values;
        public IReadOnlyList<StatValue> GetBaseStats(IPerson person) => Values;
        public void EnsureStats(IPerson person) { }
        public void SetStats(IPerson person, IReadOnlyDictionary<string, int> values) { }
        public bool TryIncreaseAcquiredStat(IPerson person, string statId) => false;
    }

    private sealed class MinimalFamilyService : IFamilyService
    {
        public void InitializePerson(IPerson person, Sex sex, int? generation = null) { }
        public Sex GetSex(IPerson person) => Sex.Male;
        public int? GetGeneration(IPerson person) => 1;
        public IPerson? GetFather(IPerson person) => null;
        public IPerson? GetMother(IPerson person) => null;
        public IPerson? GetSpouse(IPerson person) => null;
        public IReadOnlyList<IPerson> GetChildren(IPerson person) => [];
        public void SetParents(IPerson child, IPerson? father, IPerson? mother) { }
        public void SetSpouses(IPerson first, IPerson second, int startYear) { }
        public void EndRelationship(IPerson first, IPerson second, int endYear, string endReason, bool clearFirst = true, bool clearSecond = true) { }
        public IReadOnlyList<RelationshipHistoryInfo> GetRelationshipHistory(IPerson person) => [];
        public void SetGeneratedFamilyBackground(IPerson person, GeneratedFamilyBackgroundInfo background) { }
        public GeneratedFamilyBackgroundInfo? GetGeneratedFamilyBackground(IPerson person) => null;
        public string FormatSurname(string surname, Sex sex) => surname;
        public string GetDisplayName(IPerson person) => $"{person.Name} {person.Surname}";
        public bool IsBloodline(IPerson person) => false;
        public bool IsMaleLineage(IPerson person) => false;
    }

    private sealed class MinimalHealthData : IGameDataService
    {
        public IReadOnlyList<string> GetStringList(string relativePath) => [];
        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(string relativePath) => [];
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

    public class NullDispatchProxy : DispatchProxy
    {
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod is null
                || targetMethod.ReturnType == typeof(void))
            {
                return null;
            }

            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
