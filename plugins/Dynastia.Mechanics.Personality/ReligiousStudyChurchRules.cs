using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Personality;

internal sealed class ReligiousStudyChurchRules
{
    private const string DataPath =
        "LocalSociety/religious_study_church_rules.json";

    private ReligiousStudyChurchRules(
        decimal baseCost,
        IReadOnlyDictionary<int, double> tierChances)
    {
        BaseCost = baseCost;
        TierChances = tierChances;
    }

    public decimal BaseCost { get; }

    public IReadOnlyDictionary<int, double> TierChances { get; }

    public static ReligiousStudyChurchRules Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var root = JsonSerializer.Deserialize<Root>(
            data.ReadText(DataPath),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })
            ?? throw new InvalidDataException(
                $"{DataPath}: invalid JSON root.");

        if (!string.Equals(
                root.ActionId,
                "personality.religious_study",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"{DataPath}: unexpected actionId '{root.ActionId}'.");
        }

        if (root.BaseCost != 3000m)
            throw new InvalidDataException(
                $"{DataPath}: Religious Study base cost must remain 3,000 zł.");

        var chances = root.TierChances
            .Select(pair =>
            {
                if (!int.TryParse(pair.Key, out var tier)
                    || tier is < 1 or > 5
                    || pair.Value is < 0 or > 1)
                {
                    throw new InvalidDataException(
                        $"{DataPath}: invalid Church tier chance '{pair.Key}={pair.Value}'.");
                }

                return new KeyValuePair<int, double>(tier, pair.Value);
            })
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        if (!Enumerable.Range(1, 5).All(chances.ContainsKey))
            throw new InvalidDataException(
                $"{DataPath}: expected Religious Study chances for Church tiers 1-5.");

        for (var tier = 1; tier <= 5; tier++)
        {
            var expected = 0.40 + 0.05 * tier;
            if (Math.Abs(chances[tier] - expected) > 0.000001)
            {
                throw new InvalidDataException(
                    $"{DataPath}: Tier {tier} must use success chance {expected:P0}.");
            }
        }

        return new ReligiousStudyChurchRules(
            root.BaseCost,
            chances);
    }

    public double GetSuccessChance(int churchTier) =>
        TierChances.TryGetValue(churchTier, out var chance)
            ? chance
            : 0;

    private sealed class Root
    {
        public string ActionId { get; set; } = string.Empty;

        public decimal BaseCost { get; set; }

        public Dictionary<string, double> TierChances { get; set; } = [];
    }
}
