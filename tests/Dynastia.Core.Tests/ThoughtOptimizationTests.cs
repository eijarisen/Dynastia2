using System.Reflection;
using Dynastia.Contracts;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Thoughts;

namespace Dynastia.Core.Tests;

public sealed class ThoughtOptimizationTests
{
    [Fact]
    public void PluginDoesNotReconcileThoughtsAfterQueuedActions()
    {
        var lifecycle =
            new RecordingReconciliationLifecycle();

        var systems =
            new RecordingYearSystemRegistry();

        var context =
            new TestPluginContext();

        var gameState =
            new GameState();

        context.AddService<IGameState>(gameState);
        context.AddService<IPersonLookup>(gameState);
        context.AddService<IFamilyService>(CreateProxy<IFamilyService>());
        context.AddService<IStatsService>(CreateProxy<IStatsService>());
        context.AddService<IHealthService>(CreateProxy<IHealthService>());
        context.AddService<ICareerService>(CreateProxy<ICareerService>());
        context.AddService<ICraftService>(CreateProxy<ICraftService>());
        context.AddService<IFarmingService>(CreateProxy<IFarmingService>());
        context.AddService<IHouseholdService>(CreateProxy<IHouseholdService>());
        context.AddService<IJusticeService>(CreateProxy<IJusticeService>());
        context.AddService<IEducationService>(CreateProxy<IEducationService>());
        context.AddService<IEconomyService>(CreateProxy<IEconomyService>());
        context.AddService<IAdoptionService>(CreateProxy<IAdoptionService>());
        context.AddService<IMarriageSatisfactionService>(CreateProxy<IMarriageSatisfactionService>());
        context.AddService<IGameEventBus>(new GameEventBus());
        context.AddService<IYearSystemRegistry>(systems);
        context.AddService<IStateReconciliationLifecycle>(lifecycle);

        new ThoughtsPlugin().Initialize(context);

        var stages =
            lifecycle.Registrations["thoughts.current_state"];

        Assert.Contains(
            ReconciliationLifecycleStage.AfterNewGame,
            stages);
        Assert.Contains(
            ReconciliationLifecycleStage.AfterYear,
            stages);
        Assert.Contains(
            ReconciliationLifecycleStage.AfterImmediateAction,
            stages);
        Assert.Contains(
            ReconciliationLifecycleStage.AfterPersonCreated,
            stages);
        Assert.DoesNotContain(
            ReconciliationLifecycleStage.AfterQueuedAction,
            stages);

        Assert.Contains(
            systems.Systems,
            system =>
                system.Id == "thoughts.generate");
    }

    [Fact]
    public void CurrentThoughtGenerationSharesOneContextAndResolvesDynastyAnchorOnce()
    {
        var state =
            new GameState
            {
                DynastySurname = "Test",
                Year = 1800
            };

        state.CreatePerson(
            "Adam",
            "Test",
            10,
            Guid.Parse("00000000-0000-0000-0000-000000000001"));

        state.CreatePerson(
            "Jan",
            "Test",
            10,
            Guid.Parse("00000000-0000-0000-0000-000000000002"));

        var family =
            CreateFamilyProxy();

        var provider =
            new RecordingProvider(
                Candidate(
                    "shared",
                    "shared",
                    50,
                    "provider"));

        var service =
            CreateService(
                state,
                family.Service,
                new GameEventBus(),
                provider);

        service.EnsureCurrentThoughts();

        Assert.Equal(
            2,
            provider.Contexts.Count);
        Assert.Same(
            provider.Contexts[0],
            provider.Contexts[1]);
        Assert.Empty(
            provider.Contexts[0].Events);
        Assert.Equal(
            1,
            family.Proxy.GetGenerationCalls);
    }

    [Fact]
    public void AnnualThoughtGenerationSharesPendingEventContext()
    {
        var state =
            new GameState
            {
                DynastySurname = "Test",
                Year = 1801
            };

        state.CreatePerson(
            "Adam",
            "Test",
            10,
            Guid.Parse("00000000-0000-0000-0000-000000000011"));

        state.CreatePerson(
            "Jan",
            "Test",
            10,
            Guid.Parse("00000000-0000-0000-0000-000000000012"));

        var gameEvent =
            new GameEvent
            {
                Type = "test.event",
                Year = 1801,
                SubjectId = state.People[0].Id
            };

        var events =
            new GameEventBus();

        events.Publish(gameEvent);

        var family =
            CreateFamilyProxy();

        var provider =
            new RecordingProvider(
                Candidate(
                    "event",
                    "event",
                    50,
                    "event"));

        var service =
            CreateService(
                state,
                family.Service,
                events,
                provider);

        service.GenerateAnnualThoughts();

        Assert.Equal(
            2,
            provider.Contexts.Count);
        Assert.Same(
            provider.Contexts[0],
            provider.Contexts[1]);
        Assert.Single(
            provider.Contexts[0].Events);
        Assert.Same(
            gameEvent,
            provider.Contexts[0].Events[0]);
        Assert.Equal(
            1,
            family.Proxy.GetGenerationCalls);
    }

    [Fact]
    public void OnePassSelectionMatchesStableLinqTieBehavior()
    {
        var state =
            new GameState
            {
                DynastySurname = "Test",
                Year = 1800
            };

        var person =
            state.CreatePerson(
                "Adam",
                "Test",
                10,
                Guid.Parse("00000000-0000-0000-0000-000000000021"));

        var provider =
            new RecordingProvider(
                Candidate(
                    "zeta",
                    "group-a-first",
                    10,
                    "A-low",
                    deduplicationKey: "group-a"),
                Candidate(
                    "alpha",
                    "group-b",
                    60,
                    "B",
                    deduplicationKey: "group-b"),
                Candidate(
                    "ALPHA",
                    "group-a-replacement",
                    60,
                    "A",
                    deduplicationKey: "GROUP-A"));

        var service =
            CreateService(
                state,
                CreateFamilyProxy().Service,
                new GameEventBus(),
                provider);

        service.EnsureCurrentThoughts();

        var thought =
            service.GetCurrentThought(person);

        Assert.NotNull(thought);
        Assert.Equal(
            "ALPHA",
            thought.ThoughtId);
        Assert.Equal(
            "group-a-replacement",
            thought.Topic);
        Assert.Equal(
            "A",
            thought.SourceId);
        Assert.Equal(
            60,
            thought.Salience);
        Assert.Equal(
            "A",
            thought.Text);
    }

    private static StandardThoughtService CreateService(
        IGameState state,
        IFamilyService family,
        IGameEventBus events,
        params IThoughtProvider[] providers)
    {
        var registry =
            new ThoughtProviderRegistry();

        foreach (var provider in providers)
            registry.Register(provider);

        return new StandardThoughtService(
            state,
            family,
            CreateProxy<IStatsService>(),
            CreateProxy<IHealthService>(),
            CreateProxy<ICareerService>(),
            CreateProxy<IHouseholdService>(),
            CreateProxy<IJusticeService>(),
            CreateProxy<IEducationService>(),
            CreateProxy<IEconomyService>(),
            CreateProxy<IAdoptionService>(),
            CreateProxy<IMarriageSatisfactionService>(),
            events,
            registry);
    }

    private static ThoughtCandidate Candidate(
        string id,
        string topic,
        int salience,
        string sourceId,
        string? deduplicationKey = null) =>
        new(
            id,
            topic,
            deduplicationKey ?? id,
            salience,
            "💭",
            "test",
            sourceId,
            "test",
            new Dictionary<string, string>
            {
                ["literalText"] = sourceId
            });

    private static T CreateProxy<T>()
        where T : class
    {
        return DispatchProxy.Create<T, DefaultDispatchProxy>();
    }

    private static FamilyProxyResult CreateFamilyProxy()
    {
        var service =
            DispatchProxy.Create<IFamilyService, RecordingFamilyProxy>();

        return new FamilyProxyResult(
            service,
            (RecordingFamilyProxy)(object)service);
    }

    private sealed record FamilyProxyResult(
        IFamilyService Service,
        RecordingFamilyProxy Proxy);

    private sealed class RecordingProvider :
        IThoughtProvider
    {
        private readonly IReadOnlyList<ThoughtCandidate> _candidates;

        public RecordingProvider(
            params ThoughtCandidate[] candidates)
        {
            _candidates = candidates;
        }

        public string Id =>
            "test.recording";

        public List<ThoughtContext> Contexts { get; } = [];

        public IEnumerable<ThoughtCandidate> GetCandidates(
            IPerson person,
            ThoughtContext context)
        {
            Contexts.Add(context);
            return _candidates;
        }
    }

    public class RecordingFamilyProxy : DispatchProxy
    {
        public int GetGenerationCalls { get; private set; }

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod is null)
                return null;

            return targetMethod.Name switch
            {
                nameof(IFamilyService.GetGeneration) =>
                    RecordGeneration(),

                nameof(IFamilyService.IsMaleLineage) =>
                    true,

                nameof(IFamilyService.GetSex) =>
                    Sex.Male,

                _ => DefaultValue(targetMethod.ReturnType)
            };
        }

        private int RecordGeneration()
        {
            GetGenerationCalls++;
            return 1;
        }
    }

    public class DefaultDispatchProxy : DispatchProxy
    {
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args) =>
            targetMethod is null
                ? null
                : DefaultValue(
                    targetMethod.ReturnType);
    }

    private static object? DefaultValue(
        Type type)
    {
        if (type == typeof(void))
            return null;

        if (type == typeof(string))
            return string.Empty;

        if (type.IsValueType)
            return Activator.CreateInstance(type);

        if (type.IsGenericType)
        {
            var definition =
                type.GetGenericTypeDefinition();

            if (definition == typeof(IReadOnlyList<>)
                || definition == typeof(IReadOnlyCollection<>)
                || definition == typeof(IEnumerable<>))
            {
                return Array.CreateInstance(
                    type.GetGenericArguments()[0],
                    0);
            }
        }

        return null;
    }

    private sealed class TestPluginContext :
        IGamePluginContext
    {
        private readonly Dictionary<Type, object> _services = [];

        public T? GetService<T>()
            where T : class =>
            _services.TryGetValue(
                typeof(T),
                out var service)
                    ? (T)service
                    : null;

        public void AddService<T>(
            T service)
            where T : class
        {
            _services[typeof(T)] = service;
        }

        public void Log(string message)
        {
        }
    }

    private sealed class RecordingReconciliationLifecycle :
        IStateReconciliationLifecycle
    {
        public Dictionary<
            string,
            IReadOnlyCollection<ReconciliationLifecycleStage>>
            Registrations { get; } =
                new(StringComparer.OrdinalIgnoreCase);

        public void Register(
            string id,
            IReadOnlyCollection<ReconciliationLifecycleStage> stages,
            Action<ReconciliationLifecycleStage> reconcile,
            int order = 0)
        {
            Registrations[id] = stages.ToArray();
        }

        public void Reconcile(
            ReconciliationLifecycleStage stage)
        {
        }
    }

    private sealed class RecordingYearSystemRegistry :
        IYearSystemRegistry
    {
        private readonly List<IYearSystem> _systems = [];

        public IReadOnlyCollection<IYearSystem> Systems =>
            _systems;

        public void Register(
            IYearSystem system)
        {
            _systems.Add(system);
        }
    }
}
