using System.Text.Json;

namespace Dynastia.Core.Tests;

public sealed class LocalSocietyConnectionsBatch5Tests
{
    [Fact]
    public void LobbyingCreatesPersistentLightweightConnectionWithoutGeneratingPerson()
    {
        var policy = Read("plugins", "Dynastia.Mechanics.Community", "CommunityPolicyService.cs");
        var connection = Read("plugins", "Dynastia.Mechanics.Community", "CommunityConnectionService.cs");
        var state = Read("plugins", "Dynastia.Mechanics.Community", "CommunityStateComponents.cs");

        Assert.Contains("state.Connections.Add(new CommunityConnectionState", policy);
        Assert.Contains("HouseholdId = householdId", policy);
        Assert.Contains("OriginPolicyId = proposal.PolicyId", policy);
        Assert.Contains("SpouseName", state);
        Assert.Contains("List<string> Children", state);
        Assert.DoesNotContain("new Person", connection);
        Assert.DoesNotContain("IPerson person =", connection);
    }

    [Fact]
    public void ConnectionRequestsAreLowProbabilityAndDamageRelationsBeforeOutcome()
    {
        using var rules = JsonDocument.Parse(Read("data", "LocalSociety", "connection_rules.json"));
        var root = rules.RootElement;
        Assert.Equal(0.08, root.GetProperty("moneyRequest").GetProperty("baseAcceptanceChance").GetDouble(), 10);
        Assert.Equal(0.03, root.GetProperty("assetRequest").GetProperty("houseBaseAcceptanceChance").GetDouble(), 10);
        Assert.Equal(0.04, root.GetProperty("assetRequest").GetProperty("farmlandBaseAcceptanceChance").GetDouble(), 10);

        var request = root.GetProperty("requestRelationshipCost");
        Assert.Equal(-3, request.GetProperty("onAnyRequest").GetProperty("sympathy").GetInt32());
        Assert.Equal(-4, request.GetProperty("onAnyRequest").GetProperty("familiarity").GetInt32());
        Assert.Equal(-5, request.GetProperty("onRefusalAdditional").GetProperty("sympathy").GetInt32());
        Assert.Equal(-3, request.GetProperty("onRefusalAdditional").GetProperty("familiarity").GetInt32());

        var service = Read("plugins", "Dynastia.Mechanics.Community", "CommunityConnectionService.cs");
        Assert.Contains("ApplyRequestBaseCost(connection);", service);
        Assert.Contains("ApplyRefusalCost(connection);", service);
        Assert.Contains("Math.Clamp(baseChance +", service);
        Assert.Contains("0d, 0.45d", service);

        var familyRules = Read("plugins", "Dynastia.Mechanics.FamilyRelations", "FamilyRelationScoreRules.cs");
        Assert.Contains("0.95", familyRules);
    }

    [Fact]
    public void AnnualConnectionScriptStaysNamesOnlyAndPoorConnectionsDisappear()
    {
        var service = Read("plugins", "Dynastia.Mechanics.Community", "CommunityConnectionService.cs");
        var system = Read("plugins", "Dynastia.Mechanics.Community", "CommunityConnectionYearSystem.cs");

        Assert.Contains("SimulateSimpleFamily(connection, age)", service);
        Assert.Contains("connection.SpouseName = GenerateName", service);
        Assert.Contains("connection.Children.Add(GenerateName", service);
        Assert.Contains("DriftWealth(connection)", service);
        Assert.Contains("connection.WealthBand.Equals(\"Poor\"", service);
        Assert.Contains("connection.IsActive = false", service);
        Assert.Contains("YearPhase.Thoughts", system);
        Assert.Contains("actions.queued.family_relations", system);
        Assert.DoesNotContain("Components.Set", service);
    }

    [Fact]
    public void RequestCausedPovertyHurtsReputationAndNetworkRenownIsCappedAtFive()
    {
        using var rules = JsonDocument.Parse(Read("data", "LocalSociety", "connection_rules.json"));
        var consequences = rules.RootElement.GetProperty("wealthConsequences");
        var network = rules.RootElement.GetProperty("networkStatus");
        Assert.Equal(-3, consequences.GetProperty("requestCausedPoorPersistentReputationPenalty").GetInt32());
        Assert.Equal(5, network.GetProperty("householdRenownBonusCap").GetDouble(), 10);

        var connections = Read("plugins", "Dynastia.Mechanics.Community", "CommunityConnectionService.cs");
        var status = Read("plugins", "Dynastia.Mechanics.Status", "StandardStatusService.cs");
        Assert.Contains("connection.request_caused_poverty", connections);
        Assert.Contains("Math.Min(_rules.NetworkRenownBonusCap, total)", connections);
        Assert.Contains("GetNetworkRenownBonus", status);
        Assert.Contains("+ networkRenown", status);
    }

    [Fact]
    public void RelationsWindowHasFamilyAndAcquaintanceTabsWithAllSevenConnectionActions()
    {
        var window = Read("src", "Dynastia.App", "Views", "FamilyRelationsWindow.axaml");
        var actions = Read("plugins", "Dynastia.Mechanics.Community", "CommunityConnectionActions.cs");

        Assert.Contains("Header=\"Family\"", window);
        Assert.Contains("Header=\"Acquaintances\"", window);
        Assert.Contains("ItemsSource=\"{Binding Connections}\"", window);
        Assert.Contains("OnConnectionActionClick", window);

        foreach (var id in new[]
        {
            "community.connection.improve",
            "community.connection.send_money",
            "community.connection.give_house",
            "community.connection.give_farmland",
            "community.connection.request_money",
            "community.connection.request_house",
            "community.connection.request_farmland"
        })
        {
            Assert.Contains($"Id = \"{id}\"", actions);
        }
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dynastia.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
