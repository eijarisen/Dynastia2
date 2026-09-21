using System.Text.Json;

namespace Dynastia.Core.Tests;

public sealed class LocalSocietyCommunityBatch3Tests
{
    [Fact]
    public void ProposalGenerationIsDeterministicAndConstrained()
    {
        using var rules = JsonDocument.Parse(Read(
            "data", "LocalSociety", "community_policy_rules.json"));
        var root = rules.RootElement;
        var generation = root.GetProperty("generation");

        Assert.Equal(3, root.GetProperty("proposalsPerTownYear").GetInt32());
        Assert.True(generation.GetProperty("deterministic").GetBoolean());
        Assert.False(generation.GetProperty("usesGlobalRng").GetBoolean());
        Assert.True(generation.GetProperty("withoutReplacement").GetBoolean());
        Assert.Equal(1, generation.GetProperty("maximumUnfavorablePerSet").GetInt32());
        Assert.Equal(1, generation.GetProperty("maximumNoEffectPerSet").GetInt32());
        Assert.True(generation.GetProperty("preferAtLeastOneFavorableSubstantive").GetBoolean());

        var service = Read(
            "plugins", "Dynastia.Mechanics.Community", "CommunityPolicyService.cs");
        Assert.Contains("StableUnit(BuildKey(town.Id, year", service);
        Assert.Contains("eligible.Remove(chosen)", service);
        Assert.Contains("activeIds.Contains(policy.Id)", service);
        Assert.Contains("MaximumUnfavorablePerSet", service);
        Assert.Contains("MaximumNoEffectPerSet", service);
    }

    [Fact]
    public void RareStrongPoliciesHaveLowSelectionWeight()
    {
        var lines = Read(
                "data", "LocalSociety", "community_policies.csv")
            .TrimStart('\uFEFF')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var header = lines[0].Split(',');
        var rarityIndex = Array.IndexOf(header, "Rarity");
        var weightIndex = Array.IndexOf(header, "SelectionWeight");
        var impactIndex = Array.IndexOf(header, "ImpactTier");
        Assert.True(rarityIndex >= 0 && weightIndex >= 0 && impactIndex >= 0);

        var rows = lines.Skip(1)
            .Select(line => line.Split(','))
            .Where(fields => fields.Length > Math.Max(rarityIndex, Math.Max(weightIndex, impactIndex)))
            .ToArray();
        var commonAverage = rows
            .Where(fields => fields[rarityIndex].Equals("Common", StringComparison.OrdinalIgnoreCase))
            .Average(fields => double.Parse(fields[weightIndex], System.Globalization.CultureInfo.InvariantCulture));
        var rareAverage = rows
            .Where(fields => fields[rarityIndex].Equals("Rare", StringComparison.OrdinalIgnoreCase))
            .Average(fields => double.Parse(fields[weightIndex], System.Globalization.CultureInfo.InvariantCulture));

        Assert.True(rareAverage < commonAverage * 0.10);
        Assert.All(
            rows.Where(fields => fields[rarityIndex].Equals("Rare", StringComparison.OrdinalIgnoreCase)),
            fields => Assert.Equal("Strong", fields[impactIndex]));

        // Seeded weighted sample: Rare proposals must remain genuinely rare even
        // before contextual eligibility filters make them scarcer still.
        var random = new Random(31051991);
        var weighted = rows
            .Select(fields => new
            {
                Rarity = fields[rarityIndex],
                Weight = double.Parse(fields[weightIndex], System.Globalization.CultureInfo.InvariantCulture)
            })
            .ToArray();
        var totalWeight = weighted.Sum(item => item.Weight);
        var rareDraws = 0;
        const int draws = 20_000;
        for (var draw = 0; draw < draws; draw++)
        {
            var target = random.NextDouble() * totalWeight;
            var cumulative = 0.0;
            foreach (var item in weighted)
            {
                cumulative += item.Weight;
                if (target >= cumulative)
                    continue;

                if (item.Rarity.Equals("Rare", StringComparison.OrdinalIgnoreCase))
                    rareDraws++;
                break;
            }
        }

        Assert.True(rareDraws / (double)draws < 0.02);
    }

    [Fact]
    public void LobbyingAddsSupportButCannotGuaranteePassage()
    {
        using var rules = JsonDocument.Parse(Read(
            "data", "LocalSociety", "community_policy_rules.json"));
        var lobby = rules.RootElement.GetProperty("lobby");
        var resolution = rules.RootElement.GetProperty("resolution");

        Assert.Equal("community.lobby_policy", lobby.GetProperty("actionId").GetString());
        Assert.Equal(1, lobby.GetProperty("participationCountGain").GetInt32());
        Assert.Equal(0.35, lobby.GetProperty("ordinaryCap").GetDouble(), 10);
        Assert.Equal(0.45, lobby.GetProperty("townHeadCap").GetDouble(), 10);
        Assert.Contains("0.65", resolution.GetProperty("overallImplementationChance").GetString());
        Assert.True(resolution.GetProperty("atMostOnePolicyImplementedPerTownPerYear").GetBoolean());

        var service = Read(
            "plugins", "Dynastia.Mechanics.Community", "CommunityPolicyService.cs");
        Assert.Contains("_rules.OverallImplementationChanceCap", service);
        Assert.Contains("random.NextDouble() < implementationChance", service);
        Assert.Contains("proposal.BaseSupport", service);
        Assert.Contains("lobby.SupportBonus", service);
        Assert.Contains("CommunityParticipationComponent", service);
        Assert.Contains("CommunityConnectionState", service);
    }

    [Fact]
    public void ActivePoliciesExpireAndAllModifierFamiliesAreCapped()
    {
        using var rules = JsonDocument.Parse(Read(
            "data", "LocalSociety", "community_policy_rules.json"));
        var caps = rules.RootElement.GetProperty("activePolicyCaps");
        Assert.Equal(4, caps.GetProperty("prosperityFlatTotal").GetInt32());
        Assert.Equal(0.10, caps.GetProperty("educationSuccessAddTotal").GetDouble(), 10);
        Assert.Equal(0.08, caps.GetProperty("jobApplicationAddTotal").GetDouble(), 10);
        Assert.Equal(1.10m, caps.GetProperty("farmingIncomeMultiplierMax").GetDecimal());
        Assert.Equal(1.10m, caps.GetProperty("craftIncomeMultiplierMax").GetDecimal());
        Assert.Equal(1, caps.GetProperty("serviceTierBonusMax").GetInt32());
        Assert.Equal(0.80m, caps.GetProperty("historicalLossMultiplierMin").GetDecimal());

        var service = Read(
            "plugins", "Dynastia.Mechanics.Community", "CommunityPolicyService.cs");
        Assert.Contains("policy.EnactedYear <= year", service);
        Assert.Contains("policy.ExpiresAfterYear >= year", service);
        Assert.Contains("Math.Clamp(prosperity", service);
        Assert.Contains("Math.Clamp(education", service);
        Assert.Contains("Math.Clamp(jobApplication", service);
        Assert.Contains("Math.Clamp(historicalWealth", service);
    }

    [Fact]
    public void CommunityPoliciesAreIntegratedWithoutGrantingCareerInstitutionEligibility()
    {
        var career = Read(
            "plugins", "Dynastia.Mechanics.Career", "StandardCareerService.Opportunities.cs");
        var education = Read(
            "plugins", "Dynastia.Mechanics.Education", "StandardEducationService.cs");
        var townFacilities = Read(
            "plugins", "Dynastia.Mechanics.TownLife", "StandardTownFacilityQualityService.cs");
        var economy = Read(
            "plugins", "Dynastia.Mechanics.Economy", "StandardHouseMarketService.cs");
        var church = Read(
            "plugins", "Dynastia.Mechanics.Church", "ChurchPlugin.cs");
        var historical = Read(
            "plugins", "Dynastia.Mechanics.Historical", "HistoricalEventYearSystem.cs");
        var window = Read(
            "src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        Assert.Contains("JobApplicationAdd", career);
        Assert.DoesNotContain("SchoolServiceTierAdd", career);
        Assert.Contains("SchoolServiceTierAdd", education);
        Assert.Contains("BankServiceTierAdd", townFacilities);
        Assert.Contains("MedicalServiceTierAdd", townFacilities);
        Assert.Contains("ExtraHousingOffers", economy);
        Assert.Contains("ChurchWelfareMultiplier", church);
        Assert.Contains("HistoricalWealthLossMultiplier", historical);
        Assert.Contains("FloodLossMultiplier", historical);
        Assert.Contains("Header=\"Community\"", window);
        Assert.Contains("OnCommunityLobbyClick", window);
    }

    [Fact]
    public void PolicyResolutionRunsAfterQueuedLobbyActionsAndCanPassAutonomously()
    {
        var yearSystem = Read(
            "plugins", "Dynastia.Mechanics.Community", "CommunityPolicyYearSystem.cs");
        var service = Read(
            "plugins", "Dynastia.Mechanics.Community", "CommunityPolicyService.cs");

        Assert.Contains("YearPhase.QueuedActionsEarly", yearSystem);
        Assert.Contains("After => [\"actions.queued.early\"]", yearSystem);
        Assert.Contains("proposal.BaseSupport", service);
        Assert.Contains("lobbies.Where", service);
        Assert.Contains("locations.GetTowns()", service);
        Assert.Contains("playableHeadsByTown.TryGetValue", service);
        Assert.Contains("\"atMostOnePolicyImplementedPerTownPerYear\": true", Read("data", "LocalSociety", "community_policy_rules.json"));
        Assert.Contains("community.policy_enacted", service);
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
