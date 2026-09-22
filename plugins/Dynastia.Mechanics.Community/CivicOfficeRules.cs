using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal sealed class CivicOfficeRules
{
    private const string Path = "LocalSociety/civic_office_rules.json";

    public int NpcAgeMin { get; init; }
    public int NpcAgeMax { get; init; }
    public int NpcRenownMin { get; init; }
    public int NpcRenownMax { get; init; }
    public int NpcReputationMin { get; init; }
    public int NpcReputationMax { get; init; }
    public int NpcCandidateCount { get; init; }
    public double NpcCandidateWeightMin { get; init; }
    public double NpcCandidateWeightMax { get; init; }
    public decimal SmallTownSalaryMultiplier { get; init; }
    public decimal TownSalaryMultiplier { get; init; }
    public decimal CitySalaryMultiplier { get; init; }
    public decimal MajorCitySalaryMultiplier { get; init; }
    public double ApprovalMoveFraction { get; init; }
    public double NeglectPenalty { get; init; }
    public double DutiesBonus { get; init; }
    public double EnactedLobbyBonus { get; init; }
    public double ProsperityImprovedBonus { get; init; }
    public double ProsperityDeclinedPenalty { get; init; }
    public double DutiesRenown { get; init; }
    public double DutiesReputation { get; init; }
    public double Age75ReplacementBonus { get; init; }
    public IReadOnlyList<ReplacementBand> ReplacementBands { get; init; } = [];

    public decimal SettlementSalaryMultiplier(SettlementClass settlementClass) =>
        settlementClass switch
        {
            SettlementClass.SmallTown => SmallTownSalaryMultiplier,
            SettlementClass.Town => TownSalaryMultiplier,
            SettlementClass.City => CitySalaryMultiplier,
            _ => MajorCitySalaryMultiplier
        };

    public double ReplacementChance(double approval, int age)
    {
        var band = ReplacementBands.FirstOrDefault(item => item.Contains(approval));
        var chance = band?.Chance ?? 0.35;
        if (age >= 75)
            chance += Age75ReplacementBonus;
        return Math.Clamp(chance, 0, 1);
    }

    public static CivicOfficeRules Load(IGameDataService data)
    {
        using var document = JsonDocument.Parse(data.ReadText(Path));
        var root = document.RootElement;
        var townHead = root.GetProperty("townHead");
        var appointment = root.GetProperty("appointment");
        var employment = root.GetProperty("employment");
        var salary = employment.GetProperty("salary");
        var multipliers = salary.GetProperty("settlementMultipliers");
        var approval = root.GetProperty("approval");
        var actions = root.GetProperty("officeActions");
        var ages = townHead.GetProperty("npcAgeRange");
        var renown = townHead.GetProperty("npcRenownRange");
        var reputation = townHead.GetProperty("npcReputationRange");
        var npcWeights = appointment.GetProperty("npcCandidateWeightRange");

        return new CivicOfficeRules
        {
            NpcAgeMin = ages[0].GetInt32(),
            NpcAgeMax = ages[1].GetInt32(),
            NpcRenownMin = renown[0].GetInt32(),
            NpcRenownMax = renown[1].GetInt32(),
            NpcReputationMin = reputation[0].GetInt32(),
            NpcReputationMax = reputation[1].GetInt32(),
            NpcCandidateCount = appointment.GetProperty("npcCandidateCount").GetInt32(),
            NpcCandidateWeightMin = npcWeights[0].GetDouble(),
            NpcCandidateWeightMax = npcWeights[1].GetDouble(),
            SmallTownSalaryMultiplier = multipliers.GetProperty("SmallTown").GetDecimal(),
            TownSalaryMultiplier = multipliers.GetProperty("Town").GetDecimal(),
            CitySalaryMultiplier = multipliers.GetProperty("City").GetDecimal(),
            MajorCitySalaryMultiplier = multipliers.GetProperty("MajorCity").GetDecimal(),
            ApprovalMoveFraction = approval.GetProperty("annualMoveTowardStatusTargetFraction").GetDouble(),
            NeglectPenalty = approval.GetProperty("noCommunityOrOfficeActionPenalty").GetDouble(),
            DutiesBonus = approval.GetProperty("performOfficeDutiesBonus").GetDouble(),
            EnactedLobbyBonus = approval.GetProperty("lobbiedPolicyEnactedBonus").GetDouble(),
            ProsperityImprovedBonus = approval.GetProperty("prosperityImprovedBonus").GetDouble(),
            ProsperityDeclinedPenalty = approval.GetProperty("prosperityDeclinedPenalty").GetDouble(),
            Age75ReplacementBonus = approval.GetProperty("age75PlusExtraReplacementChance").GetDouble(),
            DutiesRenown = actions.GetProperty("performDutiesRenown").GetDouble(),
            DutiesReputation = actions.GetProperty("performDutiesReputation").GetDouble(),
            ReplacementBands = approval.GetProperty("replacementChanceByApproval")
                .EnumerateArray()
                .Select(item => new ReplacementBand(
                    item.TryGetProperty("min", out var min) ? min.GetDouble() : null,
                    item.TryGetProperty("max", out var max) ? max.GetDouble() : null,
                    item.GetProperty("chance").GetDouble()))
                .ToArray()
        };
    }

    internal sealed record ReplacementBand(double? Minimum, double? Maximum, double Chance)
    {
        public bool Contains(double value) =>
            (!Minimum.HasValue || value >= Minimum.Value)
            && (!Maximum.HasValue || value <= Maximum.Value);
    }
}
