using Dynastia.Contracts;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Health;
using Dynastia.Mechanics.Households;
using System.Reflection;

namespace Dynastia.Core.Tests;

public sealed class PerformanceOptimizationBatch03Tests
{
    [Fact]
    public void HealthYearSystemPreparesAndClearsPreparedProvidersInRegistryOrder()
    {
        var calls = new List<string>();
        var registry = new AnnualHealthModifierRegistry();

        registry.Register(
            new RecordingPreparedModifier(
                "first",
                calls));
        registry.Register(
            new RecordingPreparedModifier(
                "second",
                calls));

        var system = CreateSystem(registry);

        system.Execute(
            new GameState());

        Assert.Equal(
            new[]
            {
                "prepare:first",
                "prepare:second",
                "clear:first",
                "clear:second"
            },
            calls);
    }

    [Fact]
    public void HealthYearSystemClearsPreparedContextWhenPreparationThrows()
    {
        var calls = new List<string>();
        var registry = new AnnualHealthModifierRegistry();

        registry.Register(
            new RecordingPreparedModifier(
                "first",
                calls));
        registry.Register(
            new RecordingPreparedModifier(
                "second",
                calls,
                throwDuringPrepare: true));

        var system = CreateSystem(registry);

        Assert.Throws<InvalidOperationException>(
            () => system.Execute(
                new GameState()));

        Assert.Equal(
            new[]
            {
                "prepare:first",
                "prepare:second",
                "clear:first",
                "clear:second"
            },
            calls);
    }

    [Fact]
    public void HealthYearSystemClearsPreparedContextWhenPersonLoopThrows()
    {
        var calls = new List<string>();
        var registry = new AnnualHealthModifierRegistry();

        registry.Register(
            new RecordingPreparedModifier(
                "prepared",
                calls));

        var state = new GameState();
        var person = state.CreatePerson(
            "Test",
            "Person",
            30);
        person.Tags.Add("state.alive");

        var system = CreateSystem(registry);

        Assert.Throws<NullReferenceException>(
            () => system.Execute(state));

        Assert.Equal(
            new[]
            {
                "prepare:prepared",
                "clear:prepared"
            },
            calls);
    }


    [Fact]
    public void HouseholdPreparedContextMatchesLiveResultsAndBuildsStatusOncePerHousehold()
    {
        var state = new GameState();
        var head = state.CreatePerson(
            "Head",
            "Test",
            40,
            Guid.Parse("00000000-0000-0000-0000-000000000201"));
        var member = state.CreatePerson(
            "Member",
            "Test",
            25,
            Guid.Parse("00000000-0000-0000-0000-000000000202"));
        head.Tags.Add("state.alive");
        member.Tags.Add("state.alive");

        var householdId = Guid.Parse(
            "00000000-0000-0000-0000-000000000299");
        var statusCalls = 0;

        var status = new HouseholdStatusSnapshot(
            head.Id,
            UnderageChildren: 0,
            BaseChildCapacity: 3,
            EffectiveChildCapacity: 3,
            NannyId: null,
            NannyName: null,
            HasNannyReference: false,
            IsLargeFamilyStrained: false,
            IsAtCapacityWarning: false,
            IsBroke: false,
            HasUnfundedBasicNeeds: false,
            Warnings: Array.Empty<string>(),
            ResidentCount: 5,
            OvercrowdingThreshold: 2,
            IsOvercrowded: true);

        var households = CreateProxy<IHouseholdService>(
            (method, args) => method.Name switch
            {
                nameof(IHouseholdService.ResolveHouseholdHead) => head,
                nameof(IHouseholdService.GetStatus) => GetStatus(),
                _ => throw new NotSupportedException(method.Name)
            });

        object? GetStatus()
        {
            statusCalls++;
            return status;
        }

        var economy = CreateProxy<IEconomyService>(
            (method, args) => method.Name switch
            {
                nameof(IEconomyService.HasHousehold) =>
                    ReferenceEquals(args![0], head),
                nameof(IEconomyService.GetHouseholdId) => householdId,
                nameof(IEconomyService.GetHouseholdMemberIds) =>
                    new[] { head.Id, member.Id },
                _ => throw new NotSupportedException(method.Name)
            });

        var family = CreateProxy<IFamilyService>(
            (method, _) => method.Name switch
            {
                nameof(IFamilyService.GetSpouse) => null,
                _ => throw new NotSupportedException(method.Name)
            });

        var provider = new HouseholdHealthModifierProvider(
            households,
            economy,
            family,
            null!,
            null!);

        var liveHead = provider.GetAnnualHealthChange(head);
        var liveMember = provider.GetAnnualHealthChange(member);
        Assert.Equal(2, statusCalls);

        statusCalls = 0;
        provider.PrepareAnnualHealthContext(state);

        Assert.Equal(liveHead, provider.GetAnnualHealthChange(head));
        Assert.Equal(liveMember, provider.GetAnnualHealthChange(member));
        Assert.Equal(1, statusCalls);

        provider.ClearAnnualHealthContext();
        Assert.Equal(liveHead, provider.GetAnnualHealthChange(head));
        Assert.Equal(2, statusCalls);
    }

    private static HealthYearSystem CreateSystem(
        IAnnualHealthModifierRegistry registry) =>
        new(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            registry);

    private static T CreateProxy<T>(
        Func<MethodInfo, object?[]?, object?> handler)
        where T : class
    {
        var service =
            DispatchProxy.Create<T, DelegateDispatchProxy>();

        ((DelegateDispatchProxy)(object)service).Handler =
            handler;

        return service;
    }

    public class DelegateDispatchProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            return Handler?.Invoke(
                targetMethod
                ?? throw new InvalidOperationException(
                    "Proxy method is unavailable."),
                args);
        }
    }

    private sealed class RecordingPreparedModifier :
        IAnnualHealthModifierProvider,
        IPreparedAnnualHealthModifierProvider
    {
        private readonly List<string> _calls;
        private readonly bool _throwDuringPrepare;

        public RecordingPreparedModifier(
            string id,
            List<string> calls,
            bool throwDuringPrepare = false)
        {
            Id = id;
            _calls = calls;
            _throwDuringPrepare = throwDuringPrepare;
        }

        public string Id { get; }

        public double GetAnnualHealthChange(
            IPerson person) =>
            0;

        public void PrepareAnnualHealthContext(
            IGameState gameState)
        {
            _calls.Add(
                $"prepare:{Id}");

            if (_throwDuringPrepare)
            {
                throw new InvalidOperationException(
                    "Expected prepare failure.");
            }
        }

        public void ClearAnnualHealthContext()
        {
            _calls.Add(
                $"clear:{Id}");
        }
    }
}
