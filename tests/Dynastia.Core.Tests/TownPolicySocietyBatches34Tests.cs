using System.Reflection;
using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Community;
using Dynastia.Mechanics.Justice;

namespace Dynastia.Core.Tests;

public sealed class TownPolicySocietyBatches34Tests
{
    [Theory]
    [InlineData(0, 0.00)]
    [InlineData(1, 0.15)]
    [InlineData(5, 0.75)]
    [InlineData(10, 1.25)]
    [InlineData(20, 1.75)]
    [InlineData(25, 2.00)]
    [InlineData(40, 2.00)]
    public void NetworkBreadthRenownUsesPiecewiseRatesAndCap(int activeCount, double expected)
    {
        var rules = LoadConnectionRules();
        var connections = Enumerable.Range(0, activeCount)
            .Select(index => Connection($"contact-{index}", "Neutral", 0))
            .ToArray();

        var snapshot = CommunityConnectionService.CalculateNetworkSnapshot(connections, rules);

        Assert.Equal(activeCount, snapshot.ActiveCount);
        Assert.Equal(expected, snapshot.BreadthRenownBonus, 10);
    }

    [Fact]
    public void NetworkQualityAndTotalCapsPreserveQualityValueWithoutLettingCollectionRunAway()
    {
        var rules = LoadConnectionRules();
        var connections = Enumerable.Range(0, 10)
            .Select(index => Connection($"notable-{index}", "Close", 90))
            .ToArray();

        var snapshot = CommunityConnectionService.CalculateNetworkSnapshot(connections, rules);

        Assert.Equal(10, snapshot.CloseCount);
        Assert.Equal(1.25, snapshot.BreadthRenownBonus, 10);
        Assert.Equal(4.0, snapshot.QualityRenownBonus, 10);
        Assert.Equal(5.0, snapshot.TotalRenownBonus, 10);
    }

    [Fact]
    public void InactiveAndDeadConnectionsDoNotCountAndCivicBreadthCapsAtTwelve()
    {
        var rules = LoadConnectionRules();
        var contacts = new List<HouseholdConnectionInfo>();
        for (var index = 0; index < 12; index++)
            contacts.Add(Connection($"active-{index}", "Neutral", 0));
        contacts.Add(Connection("inactive", "Close", 90) with { IsActive = false });
        contacts.Add(Connection("dead", "Close", 90) with { DeathYear = 1920 });

        contacts.Add(Connection("remote-active", "Neutral", 0, townId: "remote-town"));
        var snapshot = CommunityConnectionService.CalculateLocalNetworkSnapshot(
            contacts,
            "test-town",
            rules);

        Assert.Equal(12, snapshot.ActiveCount);
        Assert.Equal(18.0, snapshot.CivicCandidateWeightBonus, 10);
        Assert.Equal(
            18.0,
            CommunityConnectionService.CalculateCivicCandidateWeightBonus(25, rules),
            10);

        var civic = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Community", "CivicOfficeService.cs");
        Assert.Contains("GetLocalNetworkSnapshot", civic);
        Assert.Contains("+ localNetworkBonus", civic);
    }

    [Theory]
    [InlineData("Neutral", 80, 0.00)]
    [InlineData("Warm", 40, 0.30)]
    [InlineData("Warm", 60, 0.36)]
    [InlineData("Warm", 80, 0.42)]
    [InlineData("Close", 40, 0.50)]
    [InlineData("Close", 60, 0.60)]
    [InlineData("Close", 80, 0.70)]
    public void LawyerAcquaintanceContributionUsesRelationAndRenown(
        string relation,
        double renown,
        double expected)
    {
        var rules = LoadCourtRules().Protection.LawyerAcquaintances;

        var score = StandardJusticeService.CalculateLawyerAcquaintanceContribution(
            relation,
            renown,
            rules);

        Assert.Equal(expected, score, 10);
    }

    [Fact]
    public void CourtProtectionUsesOnlyActiveLocalWarmOrCloseLawyersAndCapsTheirCombinedScore()
    {
        var householdId = Guid.Parse("72c0a89d-c9f6-48c4-977a-49744519a687");
        var town = new TownInfo("Test Town", "Test County", 19, 52, 20_000)
        {
            Id = "test-town"
        };
        var state = new GameState();
        var person = state.CreatePerson("Jan", "Test", 40);
        var contacts = new StubConnections(
        [
            Connection("warm-lawyer", "Warm", 40, archetypeId: "lawyer"),
            Connection("close-lawyer-a", "Close", 80, archetypeId: "lawyer"),
            Connection("close-lawyer-b", "Close", 80, archetypeId: "lawyer"),
            Connection("remote-lawyer", "Close", 80, townId: "remote-town", archetypeId: "lawyer"),
            Connection("accountant", "Close", 90, archetypeId: "accountant"),
            Connection("inactive-lawyer", "Close", 90, archetypeId: "lawyer") with { IsActive = false },
            Connection("dead-lawyer", "Close", 90, archetypeId: "lawyer") with { DeathYear = 1910 }
        ]);
        var economy = Proxy<IEconomyService>((method, _) => method.Name switch
        {
            nameof(IEconomyService.GetHouseholdId) => householdId,
            nameof(IEconomyService.GetResidenceTown) => town,
            _ => Default(method.ReturnType)
        });
        var justice = new StandardJusticeService(
            state,
            Proxy<IFamilyService>((method, _) => Default(method.ReturnType)),
            Proxy<ICareerService>((method, _) => Default(method.ReturnType)),
            LoadCourtRules(),
            () => null,
            economy,
            () => contacts);

        var listed = justice.GetCourtProtectionAcquaintances(person);
        var protection = justice.GetCourtProtection(person);

        Assert.Equal(3, listed.Count);
        Assert.DoesNotContain(listed, item => item.Name == "remote-lawyer");
        Assert.DoesNotContain(listed, item => item.Name == "accountant");
        Assert.Equal(1.50, protection.AcquaintanceScore, 10);
        Assert.Equal(0.0, protection.RelativeScore, 10);
        Assert.Equal(1.50, protection.Score, 10);
        Assert.Equal("minor", protection.TierId);
    }

    [Fact]
    public void CourtPresentationSeparatesRelativesAndLawyerAcquaintances()
    {
        var model = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var window = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        Assert.Contains("GetCourtProtectionAcquaintances(subject)", model);
        Assert.Contains("Lawyer acquaintances", window);
        Assert.Contains("No lawyer acquaintances currently provide protection.", window);
        Assert.Contains("ProtectionAcquaintances", window);
    }

    private static CommunityConnectionRules LoadConnectionRules() =>
        CommunityConnectionRules.Load(new JsonGameDataService(RepositoryFiles.Path("data")));

    private static CourtJusticeRules LoadCourtRules() =>
        CourtJusticeRules.Load(new JsonGameDataService(RepositoryFiles.Path("data")));

    private static HouseholdConnectionInfo Connection(
        string name,
        string relationState,
        double renown,
        string townId = "test-town",
        string archetypeId = "citizen") =>
        new(
            Guid.NewGuid(),
            Guid.Empty,
            name,
            Sex.Male,
            40,
            null,
            "polish",
            townId,
            archetypeId.Equals("lawyer", StringComparison.OrdinalIgnoreCase) ? "Lawyer" : "Citizen",
            archetypeId,
            "Comfortable",
            renown,
            0,
            30,
            15,
            relationState,
            null,
            Array.Empty<string>(),
            false,
            false,
            string.Empty,
            true);

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, DelegateProxy>();
        ((DelegateProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static object? Default(Type type)
    {
        if (type == typeof(void))
            return null;
        if (!type.IsValueType || Nullable.GetUnderlyingType(type) is not null)
            return null;
        return Activator.CreateInstance(type);
    }

    public sealed class DelegateProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler?.Invoke(
                targetMethod ?? throw new InvalidOperationException("Proxy method is unavailable."),
                args);
    }

    private sealed class StubConnections(IReadOnlyList<HouseholdConnectionInfo> connections)
        : IHouseholdConnectionService
    {
        public IReadOnlyList<HouseholdConnectionInfo> GetConnections(
            Guid householdId,
            bool activeOnly = true) =>
            connections
                .Where(item => !activeOnly || item.IsActive)
                .ToArray();

        public HouseholdNetworkSnapshot GetNetworkSnapshot(Guid householdId) =>
            new(0, 0, 0, 0, 0, 0, 0);

        public HouseholdNetworkSnapshot GetLocalNetworkSnapshot(Guid householdId, string townId) =>
            new(0, 0, 0, 0, 0, 0, 0);

        public double GetNetworkRenownBonus(Guid householdId) => 0d;

        public decimal GetEstimatedMoneyRequestMaximum(IPerson requester, Guid connectionId) => 0m;
    }
}
