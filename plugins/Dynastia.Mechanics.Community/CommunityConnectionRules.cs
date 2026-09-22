using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal sealed class CommunityConnectionRules
{
    private const string Path = "LocalSociety/connection_rules.json";

    public int StartingFamiliarity { get; init; }
    public int StartingSympathy { get; init; }
    public int FamiliarityDecayPerYear { get; init; }
    public int SympathyDriftTowardNeutralPerYear { get; init; }
    public int ImproveFamiliarityGain { get; init; }
    public int ImproveSympathyGain { get; init; }
    public double WealthDriftChance { get; init; }
    public IReadOnlyDictionary<string, double> SpareHouseChance { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> SpareFarmlandChance { get; init; } = new Dictionary<string, double>();
    public double FarmerFarmlandMultiplier { get; init; }
    public double MoneyBaseAcceptance { get; init; }
    public decimal MinimumTransfer { get; init; }
    public IReadOnlyDictionary<string, int> MoneyMaximumLivingCostUnits { get; init; } = new Dictionary<string, int>();
    public double HouseBaseAcceptance { get; init; }
    public double FarmlandBaseAcceptance { get; init; }
    public IReadOnlyDictionary<string, double> RelationshipAcceptancePoints { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> WealthAcceptancePoints { get; init; } = new Dictionary<string, double>();
    public int RequestSympathyCost { get; init; }
    public int RequestFamiliarityCost { get; init; }
    public int RefusalSympathyCost { get; init; }
    public int RefusalFamiliarityCost { get; init; }
    public int AcceptedMoneySympathyCost { get; init; }
    public int AcceptedMoneyFamiliarityCost { get; init; }
    public int AcceptedAssetSympathyCost { get; init; }
    public int AcceptedAssetFamiliarityCost { get; init; }
    public int MoneyGiftSympathyGain { get; init; }
    public int MoneyGiftFamiliarityGain { get; init; }
    public int AssetGiftSympathyGain { get; init; }
    public int AssetGiftFamiliarityGain { get; init; }
    public int AssetRequestWealthBandLoss { get; init; }
    public int RequestCausedPoorReputationPenalty { get; init; }
    public double ProminentThreshold { get; init; }
    public double WarmBonus { get; init; }
    public double CloseBonus { get; init; }
    public double NotableThreshold { get; init; }
    public double NotableWarmBonus { get; init; }
    public double NotableCloseBonus { get; init; }
    public double NetworkRenownBonusCap { get; init; }
    public double MarriageChance { get; init; }
    public double DivorceChance { get; init; }
    public double ChildChance { get; init; }
    public int MaximumChildren { get; init; }
    public IReadOnlyList<MortalityBand> MortalityBands { get; init; } = [];

    public sealed record MortalityBand(int MinAge, int? MaxAge, double AnnualChance);

    public static CommunityConnectionRules Load(IGameDataService data)
    {
        using var document = JsonDocument.Parse(data.ReadText(Path));
        var root = document.RootElement;
        var relationship = root.GetProperty("relationship");
        var decay = relationship.GetProperty("decayPerYear");
        var improve = relationship.GetProperty("improveRelationsAction");
        var assets = root.GetProperty("abstractAssets");
        var moneyRequest = root.GetProperty("moneyRequest");
        var assetRequest = root.GetProperty("assetRequest");
        var modifiers = root.GetProperty("acceptanceModifiersPercentagePoints");
        var requestCost = root.GetProperty("requestRelationshipCost");
        var giftGain = root.GetProperty("giftRelationshipGain");
        var consequences = root.GetProperty("wealthConsequences");
        var network = root.GetProperty("networkStatus");
        var family = root.GetProperty("simpleFamily");

        return new CommunityConnectionRules
        {
            StartingFamiliarity = relationship.GetProperty("startingFamiliarity").GetInt32(),
            StartingSympathy = relationship.GetProperty("startingSympathy").GetInt32(),
            FamiliarityDecayPerYear = decay.GetProperty("familiarity").GetInt32(),
            SympathyDriftTowardNeutralPerYear = decay.GetProperty("sympathyTowardNeutral").GetInt32(),
            ImproveFamiliarityGain = improve.GetProperty("familiarityGain").GetInt32(),
            ImproveSympathyGain = improve.GetProperty("sympathyGain").GetInt32(),
            WealthDriftChance = root.GetProperty("annualWealthDrift").GetProperty("chanceOfChange").GetDouble(),
            SpareHouseChance = ReadDoubleMap(assets.GetProperty("spareHouseChance")),
            SpareFarmlandChance = ReadDoubleMap(assets.GetProperty("spareFarmlandChance")),
            FarmerFarmlandMultiplier = assets.GetProperty("farmerOrLandownerFarmlandChanceMultiplier").GetDouble(),
            MoneyBaseAcceptance = moneyRequest.GetProperty("baseAcceptanceChance").GetDouble(),
            MinimumTransfer = moneyRequest.GetProperty("minimumTransfer").GetDecimal(),
            MoneyMaximumLivingCostUnits = ReadIntMap(moneyRequest.GetProperty("estimatedMaximumByWealthBandInLivingCostUnits")),
            HouseBaseAcceptance = assetRequest.GetProperty("houseBaseAcceptanceChance").GetDouble(),
            FarmlandBaseAcceptance = assetRequest.GetProperty("farmlandBaseAcceptanceChance").GetDouble(),
            RelationshipAcceptancePoints = ReadDoubleMap(modifiers.GetProperty("relationship")),
            WealthAcceptancePoints = ReadDoubleMap(modifiers.GetProperty("wealthBand")),
            RequestSympathyCost = requestCost.GetProperty("onAnyRequest").GetProperty("sympathy").GetInt32(),
            RequestFamiliarityCost = requestCost.GetProperty("onAnyRequest").GetProperty("familiarity").GetInt32(),
            RefusalSympathyCost = requestCost.GetProperty("onRefusalAdditional").GetProperty("sympathy").GetInt32(),
            RefusalFamiliarityCost = requestCost.GetProperty("onRefusalAdditional").GetProperty("familiarity").GetInt32(),
            AcceptedMoneySympathyCost = requestCost.GetProperty("onAcceptedMoneyAdditional").GetProperty("sympathy").GetInt32(),
            AcceptedMoneyFamiliarityCost = requestCost.GetProperty("onAcceptedMoneyAdditional").GetProperty("familiarity").GetInt32(),
            AcceptedAssetSympathyCost = requestCost.GetProperty("onAcceptedMajorAssetAdditional").GetProperty("sympathy").GetInt32(),
            AcceptedAssetFamiliarityCost = requestCost.GetProperty("onAcceptedMajorAssetAdditional").GetProperty("familiarity").GetInt32(),
            MoneyGiftSympathyGain = giftGain.GetProperty("money").GetProperty("sympathy").GetInt32(),
            MoneyGiftFamiliarityGain = giftGain.GetProperty("money").GetProperty("familiarity").GetInt32(),
            AssetGiftSympathyGain = giftGain.GetProperty("houseOrFarmland").GetProperty("sympathy").GetInt32(),
            AssetGiftFamiliarityGain = giftGain.GetProperty("houseOrFarmland").GetProperty("familiarity").GetInt32(),
            AssetRequestWealthBandLoss = consequences.GetProperty("acceptedHouseOrFarmlandLowersBandBy").GetInt32(),
            RequestCausedPoorReputationPenalty = consequences.GetProperty("requestCausedPoorPersistentReputationPenalty").GetInt32(),
            ProminentThreshold = network.GetProperty("prominentConnectionThreshold").GetDouble(),
            WarmBonus = network.GetProperty("warmBonus").GetDouble(),
            CloseBonus = network.GetProperty("closeBonus").GetDouble(),
            NotableThreshold = network.GetProperty("notableConnectionThreshold").GetDouble(),
            NotableWarmBonus = network.GetProperty("notableWarmBonus").GetDouble(),
            NotableCloseBonus = network.GetProperty("notableCloseBonus").GetDouble(),
            NetworkRenownBonusCap = network.GetProperty("householdRenownBonusCap").GetDouble(),
            MarriageChance = family.GetProperty("unmarriedAnnualMarriageChance").GetDouble(),
            DivorceChance = family.GetProperty("marriedAnnualDivorceChance").GetDouble(),
            ChildChance = family.GetProperty("annualChildChanceIfPlausible").GetDouble(),
            MaximumChildren = family.GetProperty("maximumChildren").GetInt32(),
            MortalityBands = family.GetProperty("mortalityByAge")
                .EnumerateArray()
                .Select(item => new MortalityBand(
                    item.GetProperty("minAge").GetInt32(),
                    item.TryGetProperty("maxAge", out var max) ? max.GetInt32() : null,
                    item.GetProperty("annualChance").GetDouble()))
                .ToArray()
        };
    }

    private static IReadOnlyDictionary<string, double> ReadDoubleMap(JsonElement element) =>
        element.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.GetDouble(),
            StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, int> ReadIntMap(JsonElement element) =>
        element.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.GetInt32(),
            StringComparer.OrdinalIgnoreCase);
}
