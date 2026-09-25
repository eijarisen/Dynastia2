using System.Reflection;
using Dynastia.Contracts;
using Dynastia.Core.Data;
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
        context.AddService<IGameDataService>(
            new JsonGameDataService(
                RepositoryFiles.Path("data")));
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


    [Fact]
    public void LegacyThoughtComponentIsRegeneratedWithoutReplayingEvents()
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
                30,
                Guid.Parse("00000000-0000-0000-0000-000000000031"));

#pragma warning disable CS0618
        person.Components.Set(
            new PersonThoughtComponent
            {
                Year = 1800,
                ThoughtId = "legacy",
                Topic = "household.farming",
                Text = "Legacy",
                Emoji = "🌾",
                Salience = 50
            });
#pragma warning restore CS0618

        person.Components.Set(
            new ThoughtSystemStateComponent
            {
                LastProcessedEventCount = 1,
                LastGeneratedYear = 1800
            });

        var events = new GameEventBus();
        events.Publish(
            new GameEvent
            {
                Type = "old.event",
                Year = 1799,
                SubjectId = person.Id
            });

        var provider =
            new RecordingProvider(
                Candidate(
                    "fresh",
                    "household.farming",
                    50,
                    "fresh",
                    moodId: ThoughtMoodIds.Neutral,
                    topicEmoji: "🌾"));

        var service =
            CreateService(
                state,
                CreateFamilyProxy().Service,
                events,
                provider);

        service.ResetAfterLoad();

        var component =
            person.Components.Get<PersonThoughtComponent>();

        Assert.NotNull(component);
        Assert.Equal("fresh", component.ThoughtId);
        Assert.Equal(ThoughtMoodIds.Neutral, component.MoodId);
        Assert.Equal(string.Empty, component.MoodEmoji);
        Assert.Equal("🌾", component.TopicEmoji);
        Assert.Empty(provider.Contexts.Single().Events);

        var systemState =
            person.Components.Get<ThoughtSystemStateComponent>();

        Assert.NotNull(systemState);
        Assert.Equal(1, systemState.LastProcessedEventCount);
    }

    [Theory]
    [InlineData("personality.melancholic")]
    [InlineData("personality.phlegmatic")]
    [InlineData("personality.sanguine")]
    [InlineData("personality.choleric")]
    public void ExplicitPersonalityTraitsMatchLegacySalience(string temperament)
    {
        var state = new GameState();
        var person = state.CreatePerson("A", "B", 30, Guid.NewGuid());
        person.Tags.Add(temperament);

        var cases = new[]
        {
            ("career.fired", "career.work", "career.fired", ThoughtSalienceTraits.Emotional | ThoughtSalienceTraits.Negative | ThoughtSalienceTraits.Career | ThoughtSalienceTraits.ImmediateProblem | ThoughtSalienceTraits.MelancholicHighImpact),
            ("career.miserable", "career.work", "career.miserable", ThoughtSalienceTraits.Negative | ThoughtSalienceTraits.Career | ThoughtSalienceTraits.ImmediateProblem),
            ("relationship.marriage.current", "relationship.marriage", "marriage.new", ThoughtSalienceTraits.Emotional | ThoughtSalienceTraits.Positive),
            ("relationship.satisfaction", "relationship.satisfaction", "marriage.satisfaction", ThoughtSalienceTraits.Emotional),
            ("rare.assault", "rare.event", "rare.assault", ThoughtSalienceTraits.Emotional | ThoughtSalienceTraits.Negative | ThoughtSalienceTraits.ImmediateProblem | ThoughtSalienceTraits.RareTrauma | ThoughtSalienceTraits.MelancholicHighImpact),
            ("education.success", "education", "education.success", ThoughtSalienceTraits.Positive | ThoughtSalienceTraits.Career | ThoughtSalienceTraits.Education)
        };

        foreach (var testCase in cases)
        {
            var candidate = Candidate(
                testCase.Item1,
                testCase.Item2,
                80,
                "test",
                salienceTraits: testCase.Item4,
                wordingKey: testCase.Item3);

            var expected = LegacyPersonalitySalience(
                person,
                testCase.Item1,
                testCase.Item2,
                testCase.Item3,
                80);

            Assert.Equal(
                expected,
                StandardThoughtService.ApplyPersonalitySalience(
                    person,
                    candidate,
                    80));
        }
    }

    [Theory]
    [InlineData(7, 80, ThoughtSalienceTraits.FamilyLoss, 90)]
    [InlineData(7, 80, ThoughtSalienceTraits.Health, 85)]
    [InlineData(7, 80, ThoughtSalienceTraits.Poverty, 60)]
    [InlineData(7, 80, ThoughtSalienceTraits.HouseholdStrain, 70)]
    [InlineData(15, 80, ThoughtSalienceTraits.FamilyLoss, 85)]
    [InlineData(15, 80, ThoughtSalienceTraits.RareTrauma, 85)]
    [InlineData(15, 80, ThoughtSalienceTraits.Poverty, 70)]
    [InlineData(15, 80, ThoughtSalienceTraits.HouseholdStrain, 75)]
    public void ExplicitAgeTraitsPreserveLegacyPriority(
        int age,
        int baseSalience,
        ThoughtSalienceTraits traits,
        int expected)
    {
        var state = new GameState();
        var person = state.CreatePerson("A", "B", age, Guid.NewGuid());
        var candidate = Candidate(
            "renamed.id",
            "renamed.topic",
            baseSalience,
            "test",
            salienceTraits: traits,
            wordingKey: "renamed.wording");

        Assert.Equal(
            expected,
            ThoughtProviderUtilities.ApplyAgePriority(
                person,
                candidate));
    }

    [Fact]
    public void RenamingPresentationFieldsDoesNotChangeSalience()
    {
        var state = new GameState();
        var person = state.CreatePerson("A", "B", 15, Guid.NewGuid());
        person.Tags.Add("personality.melancholic");

        var traits =
            ThoughtSalienceTraits.Emotional
            | ThoughtSalienceTraits.Negative
            | ThoughtSalienceTraits.FamilyLoss;

        var first = Candidate(
            "family.loss",
            "family.loss",
            80,
            "first",
            salienceTraits: traits,
            moodId: ThoughtMoodIds.Grieving,
            topicEmoji: "👪",
            wordingKey: "family.loss.current");

        var renamed = Candidate(
            "totally.renamed",
            "different.topic",
            80,
            "second",
            salienceTraits: traits,
            moodId: ThoughtMoodIds.Happy,
            topicEmoji: "🌾",
            wordingKey: "different.wording");

        int Adjust(ThoughtCandidate candidate)
        {
            var age = ThoughtProviderUtilities.ApplyAgePriority(person, candidate);
            return StandardThoughtService.ApplyPersonalitySalience(person, candidate, age);
        }

        Assert.Equal(Adjust(first), Adjust(renamed));
    }

    [Fact]
    public void MoodAndTopicEmojiDoNotAffectWinnerSelection()
    {
        static string Select(
            string moodId,
            string topicEmoji)
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
                    30,
                    Guid.NewGuid());

            var service =
                CreateService(
                    state,
                    CreateFamilyProxy().Service,
                    new GameEventBus(),
                    new RecordingProvider(
                        Candidate(
                            "winner",
                            "topic",
                            60,
                            "winner",
                            moodId: moodId,
                            topicEmoji: topicEmoji),
                        Candidate(
                            "runner-up",
                            "topic",
                            50,
                            "runner-up",
                            moodId: ThoughtMoodIds.Neutral,
                            topicEmoji: "💼")));

            service.EnsureCurrentThoughts();
            return service.GetCurrentThought(person)!.ThoughtId;
        }

        Assert.Equal(
            Select(ThoughtMoodIds.Angry, "🌾"),
            Select(ThoughtMoodIds.Happy, "💰"));
    }

    [Fact]
    public void GenericChildFallbackThoughtCarriesATopicEmoji()
    {
        var state = new GameState
        {
            DynastySurname = "Test",
            Year = 1900
        };
        var child = state.CreatePerson(
            "Child",
            "Test",
            8,
            Guid.Parse("20000000-0000-0000-0000-000000000001"));
        child.Tags.Add("state.alive");

        var service = CreateService(
            state,
            CreateFamilyProxy().Service,
            new GameEventBus());

        service.EnsureCurrentThoughts();

        var thought = child.Components.Get<PersonThoughtComponent>();
        Assert.NotNull(thought);
        Assert.Equal("fallback", thought.ThoughtId);
        Assert.Equal(ThoughtMoodIds.Neutral, thought.MoodId);
        Assert.Equal("💭", thought.TopicEmoji);
    }

    [Fact]
    public void UnknownMoodFallsBackToNeutralPresentation()
    {
        Assert.Equal(
            ThoughtMoodIds.Neutral,
            ThoughtMoodCatalog.Normalize("external.unknown"));
        Assert.Equal(
            string.Empty,
            ThoughtMoodCatalog.ResolveEmoji("external.unknown"));
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
            registry,
            CreateTestWordingRegistry());
    }

    private static IThoughtWordingRegistry CreateTestWordingRegistry()
    {
        var wording = new ThoughtWordingRegistry();
        wording.RegisterCatalogue(
            "tests",
            new Dictionary<string, ThoughtWordingDefinition>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["fallback"] = new()
                {
                    AdultNormal = ["Things are ordinary."]
                },
                ["test"] = new()
                {
                    AdultNormal = ["Test thought."]
                }
            });
        return wording;
    }

    private static ThoughtCandidate Candidate(
        string id,
        string topic,
        int salience,
        string sourceId,
        string? deduplicationKey = null,
        ThoughtSalienceTraits salienceTraits = ThoughtSalienceTraits.None,
        string moodId = ThoughtMoodIds.Neutral,
        string topicEmoji = "💭",
        string wordingKey = "test") =>
        new(
            id,
            topic,
            deduplicationKey ?? id,
            salience,
            moodId,
            topicEmoji,
            salienceTraits,
            "test",
            sourceId,
            wordingKey,
            new Dictionary<string, string>
            {
                ["literalText"] = sourceId
            });

    private static int LegacyPersonalitySalience(
        IPerson person,
        string id,
        string topic,
        string wordingKey,
        int salience)
    {
        var key = $"{id} {topic} {wordingKey}".ToLowerInvariant();

        var emotional =
            new[] { "loss", "bereavement", "divorce", "affair", "fired", "assault", "illness", "orphan", "marriage", "birth", "relationship" }
                .Any(key.Contains);

        var negative =
            new[] { "loss", "bereavement", "divorce", "affair", "fired", "assault", "miserable", "unhappy", "broke", "illness", "orphan", "imprison", "failure" }
                .Any(key.Contains);

        var positive =
            new[] { "married", "marriage.new", "birth", "promotion", "satisfied", "thriving", "repaired", "success", "inheritance", "lottery" }
                .Any(key.Contains);

        var career =
            new[] { "career", "employment", "education" }
                .Any(key.Contains);

        var immediateProblem =
            new[] { "fired", "miserable", "unhappy", "broke", "assault", "divorce" }
                .Any(key.Contains);

        var highImpact =
            new[] { "bereavement", "divorce", "fired", "assault" }
                .Any(key.Contains);

        var multiplier = PersonalityInfluence.Multiplier(
            person,
            negative ? (highImpact ? 0.20 : 0.15) : emotional ? 0.10 : 0,
            emotional && salience < 90 ? -0.10 : 0,
            positive ? 0.10 : career ? 0.05 : 0,
            career || immediateProblem ? 0.10 : 0);

        return Math.Max(
            1,
            (int)Math.Round(
                salience * multiplier,
                MidpointRounding.AwayFromZero));
    }

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
