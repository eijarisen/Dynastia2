using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class CourtJusticeRules
{
    public ProtectionRules Protection { get; init; } = new();
    public BailRules Bail { get; init; } = new();
    public EscapeRules Escape { get; init; } = new();
    public StolenHeirloomSaleRules StolenHeirloomSale { get; init; } = new();

    public static CourtJusticeRules Load(IGameDataService data)
    {
        var rules = JsonSerializer.Deserialize<CourtJusticeRules>(
            data.ReadText("LocalSociety/court_justice_rules.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException(
                "LocalSociety/court_justice_rules.json could not be parsed.");
        rules.Validate();
        return rules;
    }

    public CourtProtectionTier ResolveProtection(double score) =>
        Protection.Tiers
            .OrderByDescending(tier => tier.MinScore)
            .FirstOrDefault(tier => score >= tier.MinScore
                && (!tier.MaxScore.HasValue || score <= tier.MaxScore.Value))
        ?? Protection.Tiers.OrderBy(tier => tier.MinScore).First();

    public decimal CalculateBailCost(int remainingSentenceYears) =>
        Math.Max(
            Bail.MinimumCost,
            Bail.BaseCost + Bail.PerRemainingYear * Math.Max(0, remainingSentenceYears));

    private void Validate()
    {
        if (Protection.Tiers.Count == 0)
            throw new InvalidOperationException("Court protection requires at least one tier.");
        if (Protection.CombinedSentenceMultiplierFloor is <= 0m or > 1m)
            throw new InvalidOperationException("Court sentence floor must be within (0,1].");
        if (Bail.MinimumCost < 0 || Bail.BaseCost < 0 || Bail.PerRemainingYear < 0)
            throw new InvalidOperationException("Court bail costs cannot be negative.");
        if (Escape.RequiresIntellect < 1)
            throw new InvalidOperationException("Escape intellect requirement is invalid.");
        if (Escape.BaseSuccessChance is < 0 or > 1
            || Escape.MastermindSuccessChance is < 0 or > 1)
        {
            throw new InvalidOperationException("Escape chances must be probabilities.");
        }
    }
}

public sealed class ProtectionRules
{
    public Dictionary<string, double> QualifyingCareerWeights { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> RelationMultipliers { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<CourtProtectionTier> Tiers { get; init; } = [];
    public decimal CombinedSentenceMultiplierFloor { get; init; } = 0.5m;
}

public sealed class CourtProtectionTier
{
    public string Id { get; init; } = "none";
    public double MinScore { get; init; }
    public double? MaxScore { get; init; }
    public string Display { get; init; } = "None";
    public decimal SentenceMultiplier { get; init; } = 1m;
    public double StolenSaleDetectionChance { get; init; } = 0.30;
}

public sealed class BailRules
{
    public string ActionId { get; init; } = "justice.bail_out";
    public decimal MinimumCost { get; init; } = 20_000m;
    public decimal BaseCost { get; init; } = 20_000m;
    public decimal PerRemainingYear { get; init; } = 7_500m;

    // The supplied JSON describes the formula as prose; these defaults are
    // authoritative for S7 and are intentionally explicit in code.
}

public sealed class EscapeRules
{
    public string ActionId { get; init; } = "justice.attempt_escape";
    public int RequiresIntellect { get; init; } = 5;
    public double BaseSuccessChance { get; init; } = 0.20;
    public double MastermindSuccessChance { get; init; } = 0.30;
    public int FailureSentenceExtensionYears { get; init; } = 3;
}

public sealed class StolenHeirloomSaleRules
{
    public bool KeepingIsHarmless { get; init; } = true;
    public bool DetectionChanceFromProtectionTier { get; init; } = true;
    public StolenHeirloomCaughtRules Caught { get; init; } = new();
}

public sealed class StolenHeirloomCaughtRules
{
    public int SentenceYearsMin { get; init; } = 2;
    public int SentenceYearsMax { get; init; } = 5;
    public string ReasonId { get; init; } = "selling_stolen_property";
    public string DisplayName { get; init; } = "selling stolen property";
    public double ReputationDelta { get; init; } = -8;
}
