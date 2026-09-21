using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

internal sealed class HouseMarketRules
{
    private const string Path = "Housing/house_market_rules.json";

    private readonly IReadOnlyDictionary<int, decimal> _capacityMultipliers;
    private readonly IReadOnlyDictionary<SettlementClass, IReadOnlyDictionary<int, int>> _capacityWeights;
    private readonly IReadOnlyDictionary<SettlementClass, (int Min, int Max)> _offerCountRanges;

    private HouseMarketRules(
        int standardCapacity,
        IReadOnlyDictionary<int, decimal> capacityMultipliers,
        IReadOnlyDictionary<SettlementClass, IReadOnlyDictionary<int, int>> capacityWeights,
        IReadOnlyDictionary<SettlementClass, (int Min, int Max)> offerCountRanges,
        decimal offerRandomMinimum,
        decimal offerRandomMaximum,
        decimal prosperityMinimum,
        decimal prosperityMaximum,
        decimal saleValueMultiplier)
    {
        StandardCapacity = standardCapacity;
        _capacityMultipliers = capacityMultipliers;
        _capacityWeights = capacityWeights;
        _offerCountRanges = offerCountRanges;
        OfferRandomMinimum = offerRandomMinimum;
        OfferRandomMaximum = offerRandomMaximum;
        ProsperityMinimum = prosperityMinimum;
        ProsperityMaximum = prosperityMaximum;
        SaleValueMultiplier = saleValueMultiplier;
    }

    public int StandardCapacity { get; }
    public decimal OfferRandomMinimum { get; }
    public decimal OfferRandomMaximum { get; }
    public decimal ProsperityMinimum { get; }
    public decimal ProsperityMaximum { get; }
    public decimal SaleValueMultiplier { get; }

    public static HouseMarketRules CreateDefault() =>
        new(
            6,
            new Dictionary<int, decimal>
            {
                [2] = 0.70m,
                [4] = 0.85m,
                [6] = 1.00m,
                [8] = 1.15m
            },
            new Dictionary<SettlementClass, IReadOnlyDictionary<int, int>>
            {
                [SettlementClass.SmallTown] = new Dictionary<int, int> { [2] = 35, [4] = 35, [6] = 25, [8] = 5 },
                [SettlementClass.Town] = new Dictionary<int, int> { [2] = 20, [4] = 30, [6] = 35, [8] = 15 },
                [SettlementClass.City] = new Dictionary<int, int> { [2] = 10, [4] = 25, [6] = 40, [8] = 25 },
                [SettlementClass.MajorCity] = new Dictionary<int, int> { [2] = 5, [4] = 15, [6] = 40, [8] = 40 }
            },
            new Dictionary<SettlementClass, (int Min, int Max)>
            {
                [SettlementClass.SmallTown] = (0, 2),
                [SettlementClass.Town] = (0, 3),
                [SettlementClass.City] = (1, 4),
                [SettlementClass.MajorCity] = (2, 5)
            },
            0.80m,
            1.20m,
            0.90m,
            1.10m,
            0.80m);

    public static HouseMarketRules Load(IGameDataService data)
    {
        var root = JsonSerializer.Deserialize<Root>(
            data.ReadText(Path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"{Path}: invalid JSON root.");

        if (root.StandardCapacity <= 0 || root.OfferCapacities.Count == 0)
            throw new InvalidDataException($"{Path}: capacities are missing.");

        var multipliers = root.CapacityPriceMultipliers
            .ToDictionary(
                pair => int.Parse(pair.Key, System.Globalization.CultureInfo.InvariantCulture),
                pair => pair.Value);

        foreach (var capacity in root.OfferCapacities)
        {
            if (!multipliers.TryGetValue(capacity, out var multiplier) || multiplier <= 0m)
                throw new InvalidDataException($"{Path}: invalid multiplier for capacity {capacity}.");
        }

        var weights = new Dictionary<SettlementClass, IReadOnlyDictionary<int, int>>();
        foreach (var settlement in Enum.GetValues<SettlementClass>())
        {
            if (!root.CapacityWeightsBySettlement.TryGetValue(settlement.ToString(), out var configured))
                throw new InvalidDataException($"{Path}: missing capacity weights for {settlement}.");

            var converted = configured.ToDictionary(
                pair => int.Parse(pair.Key, System.Globalization.CultureInfo.InvariantCulture),
                pair => pair.Value);
            if (converted.Values.Sum() <= 0 || converted.Any(pair => pair.Value < 0))
                throw new InvalidDataException($"{Path}: invalid capacity weights for {settlement}.");
            weights[settlement] = converted;
        }

        var ranges = new Dictionary<SettlementClass, (int Min, int Max)>();
        foreach (var settlement in Enum.GetValues<SettlementClass>())
        {
            if (!root.OfferCountRangeBySettlement.TryGetValue(settlement.ToString(), out var range)
                || range.Count != 2
                || range[0] < 0
                || range[1] < range[0])
            {
                throw new InvalidDataException($"{Path}: invalid offer-count range for {settlement}.");
            }

            ranges[settlement] = (range[0], range[1]);
        }

        if (root.OfferRandomPriceMultiplier.Count != 2
            || root.OfferRandomPriceMultiplier[0] <= 0m
            || root.OfferRandomPriceMultiplier[1] < root.OfferRandomPriceMultiplier[0])
        {
            throw new InvalidDataException($"{Path}: invalid offer-random range.");
        }

        if (root.ProsperityPrice.Minimum <= 0m
            || root.ProsperityPrice.Maximum < root.ProsperityPrice.Minimum)
        {
            throw new InvalidDataException($"{Path}: invalid prosperity multiplier range.");
        }

        if (root.Sale.SaleValueMultiplier <= 0m)
            throw new InvalidDataException($"{Path}: invalid sale multiplier.");

        return new HouseMarketRules(
            root.StandardCapacity,
            multipliers,
            weights,
            ranges,
            root.OfferRandomPriceMultiplier[0],
            root.OfferRandomPriceMultiplier[1],
            root.ProsperityPrice.Minimum,
            root.ProsperityPrice.Maximum,
            root.Sale.SaleValueMultiplier);
    }

    public decimal GetCapacityMultiplier(int capacity) =>
        _capacityMultipliers.TryGetValue(capacity, out var multiplier)
            ? multiplier
            : _capacityMultipliers[StandardCapacity];

    public decimal GetProsperityMultiplier(int prosperityIndex) =>
        Math.Clamp(
            1m + (prosperityIndex - 100) * 0.004m,
            ProsperityMinimum,
            ProsperityMaximum);

    public (int Min, int Max) GetOfferCountRange(SettlementClass settlement) =>
        _offerCountRanges[settlement];

    public int DrawCapacity(SettlementClass settlement, double unitRoll)
    {
        var weights = _capacityWeights[settlement];
        var total = weights.Values.Sum();
        var roll = Math.Clamp(unitRoll, 0.0, 0.9999999999999999) * total;
        var cumulative = 0.0;

        foreach (var capacity in weights.Keys.OrderBy(value => value))
        {
            cumulative += weights[capacity];
            if (roll < cumulative)
                return capacity;
        }

        return weights.Keys.Max();
    }

    private sealed class Root
    {
        public int StandardCapacity { get; set; }
        public List<int> OfferCapacities { get; set; } = [];
        public Dictionary<string, decimal> CapacityPriceMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Dictionary<string, int>> CapacityWeightsBySettlement { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<int>> OfferCountRangeBySettlement { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<decimal> OfferRandomPriceMultiplier { get; set; } = [];
        public ProsperityPriceDto ProsperityPrice { get; set; } = new();
        public SaleDto Sale { get; set; } = new();
    }

    private sealed class ProsperityPriceDto
    {
        public decimal Minimum { get; set; }
        public decimal Maximum { get; set; }
    }

    private sealed class SaleDto
    {
        public decimal SaleValueMultiplier { get; set; }
    }
}
