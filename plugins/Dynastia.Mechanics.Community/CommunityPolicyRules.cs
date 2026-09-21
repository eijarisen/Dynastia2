using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal sealed class CommunityPolicyRules
{
    private const string Path = "LocalSociety/community_policy_rules.json";

    public int ProposalsPerTownYear { get; init; } = 3;
    public int MaximumUnfavorablePerSet { get; init; } = 1;
    public int MaximumNoEffectPerSet { get; init; } = 1;
    public bool PreferAtLeastOneFavorableSubstantive { get; init; } = true;
    public double OverallImplementationChanceCap { get; init; } = 0.65;
    public double LobbyRenownGain { get; init; } = 0.25;
    public double LobbyReputationGain { get; init; } = 0.1;
    public double EnactedExtraRenown { get; init; } = 0.75;
    public double EnactedExtraReputation { get; init; } = 0.3;
    public int ParticipationCountGain { get; init; } = 1;
    public double TownHeadOfficeBonus { get; init; } = 0.1;
    public double OrdinaryLobbyCap { get; init; } = 0.35;
    public double TownHeadLobbyCap { get; init; } = 0.45;

    public int ProsperityFlatCap { get; init; } = 4;
    public double EducationSuccessAddCap { get; init; } = 0.1;
    public double JobApplicationAddCap { get; init; } = 0.08;
    public decimal FarmingIncomeMultiplierMax { get; init; } = 1.1m;
    public decimal CraftIncomeMultiplierMax { get; init; } = 1.1m;
    public decimal MedicalCostMultiplierMin { get; init; } = 0.8m;
    public decimal MedicalCostMultiplierMax { get; init; } = 1.15m;
    public decimal BankQualityMultiplierMin { get; init; } = 0.9m;
    public decimal BankQualityMultiplierMax { get; init; } = 1.1m;
    public decimal HousingPriceMultiplierMin { get; init; } = 0.9m;
    public decimal HousingPriceMultiplierMax { get; init; } = 1.1m;
    public int ServiceTierBonusMax { get; init; } = 1;
    public decimal HistoricalLossMultiplierMin { get; init; } = 0.8m;

    public static CommunityPolicyRules Load(IGameDataService data)
    {
        using var document = JsonDocument.Parse(data.ReadText(Path));
        var root = document.RootElement;
        var generation = root.GetProperty("generation");
        var resolution = root.GetProperty("resolution");
        var lobby = root.GetProperty("lobby");
        var caps = root.GetProperty("activePolicyCaps");

        var implementationExpression = resolution.GetProperty("overallImplementationChance").GetString() ?? "";
        var implementationCap = ParseMinCap(implementationExpression, 0.65);

        return new CommunityPolicyRules
        {
            ProposalsPerTownYear = root.GetProperty("proposalsPerTownYear").GetInt32(),
            MaximumUnfavorablePerSet = generation.GetProperty("maximumUnfavorablePerSet").GetInt32(),
            MaximumNoEffectPerSet = generation.GetProperty("maximumNoEffectPerSet").GetInt32(),
            PreferAtLeastOneFavorableSubstantive = generation.GetProperty("preferAtLeastOneFavorableSubstantive").GetBoolean(),
            OverallImplementationChanceCap = implementationCap,
            ParticipationCountGain = lobby.GetProperty("participationCountGain").GetInt32(),
            LobbyRenownGain = lobby.GetProperty("renownGain").GetDouble(),
            LobbyReputationGain = lobby.GetProperty("reputationGain").GetDouble(),
            EnactedExtraRenown = lobby.GetProperty("enactedExtraRenown").GetDouble(),
            EnactedExtraReputation = lobby.GetProperty("enactedExtraReputation").GetDouble(),
            TownHeadOfficeBonus = lobby.GetProperty("townHeadOfficeBonus").GetDouble(),
            OrdinaryLobbyCap = lobby.GetProperty("ordinaryCap").GetDouble(),
            TownHeadLobbyCap = lobby.GetProperty("townHeadCap").GetDouble(),
            ProsperityFlatCap = caps.GetProperty("prosperityFlatTotal").GetInt32(),
            EducationSuccessAddCap = caps.GetProperty("educationSuccessAddTotal").GetDouble(),
            JobApplicationAddCap = caps.GetProperty("jobApplicationAddTotal").GetDouble(),
            FarmingIncomeMultiplierMax = caps.GetProperty("farmingIncomeMultiplierMax").GetDecimal(),
            CraftIncomeMultiplierMax = caps.GetProperty("craftIncomeMultiplierMax").GetDecimal(),
            MedicalCostMultiplierMin = caps.GetProperty("medicalCostMultiplierMin").GetDecimal(),
            MedicalCostMultiplierMax = caps.GetProperty("medicalCostMultiplierMax").GetDecimal(),
            BankQualityMultiplierMin = caps.GetProperty("bankQualityMultiplierMin").GetDecimal(),
            BankQualityMultiplierMax = caps.GetProperty("bankQualityMultiplierMax").GetDecimal(),
            HousingPriceMultiplierMin = caps.GetProperty("housingPriceMultiplierMin").GetDecimal(),
            HousingPriceMultiplierMax = caps.GetProperty("housingPriceMultiplierMax").GetDecimal(),
            ServiceTierBonusMax = caps.GetProperty("serviceTierBonusMax").GetInt32(),
            HistoricalLossMultiplierMin = caps.GetProperty("historicalLossMultiplierMin").GetDecimal()
        };
    }

    public double CalculateLobbyBonus(
        double localRenown,
        double reputation,
        int education,
        int appeal,
        bool isTownHead)
    {
        var officeBonus = isTownHead ? TownHeadOfficeBonus : 0.0;
        var cap = isTownHead ? TownHeadLobbyCap : OrdinaryLobbyCap;
        var raw = localRenown * 0.003
            + reputation * 0.001
            + education * 0.008
            + appeal * 0.004
            + officeBonus;
        return Math.Clamp(raw, -0.05, cap);
    }

    private static double ParseMinCap(string expression, double fallback)
    {
        const string prefix = "min(";
        if (!expression.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return fallback;
        var comma = expression.IndexOf(',');
        if (comma <= prefix.Length)
            return fallback;
        var raw = expression[prefix.Length..comma].Trim();
        return double.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
