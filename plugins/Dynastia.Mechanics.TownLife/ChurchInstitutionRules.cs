using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class ChurchInstitutionRules
{
    private const string DataPath = "LocalSociety/church_rules.json";

    private readonly IReadOnlyList<TierRule> _tiers;

    private ChurchInstitutionRules(IReadOnlyList<TierRule> tiers)
    {
        _tiers = tiers;
    }

    public static ChurchInstitutionRules Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var root = JsonSerializer.Deserialize<Root>(
            data.ReadText(DataPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"{DataPath}: invalid JSON root.");

        if (!root.AlwaysPresent)
            throw new InvalidDataException($"{DataPath}: Church must be configured as always present.");

        var tiers = root.TierByPopulation
            .OrderBy(rule => rule.MinPopulation ?? 0)
            .Select(rule => new TierRule(
                rule.MinPopulation ?? 0,
                rule.MaxPopulation,
                rule.Tier,
                rule.DisplayName))
            .ToArray();

        if (tiers.Length == 0
            || tiers.Any(rule => rule.Tier is < 1 or > 5)
            || tiers.Any(rule => string.IsNullOrWhiteSpace(rule.DisplayName)))
        {
            throw new InvalidDataException($"{DataPath}: invalid Church population tiers.");
        }

        if (tiers[0].MinPopulation != 0
            || tiers[^1].MaxPopulation is not null)
        {
            throw new InvalidDataException($"{DataPath}: Church population tiers must cover every population.");
        }

        for (var index = 0; index < tiers.Length; index++)
        {
            var current = tiers[index];
            if (current.MaxPopulation is int maximum
                && maximum < current.MinPopulation)
            {
                throw new InvalidDataException($"{DataPath}: invalid Church population range.");
            }

            if (index == 0)
                continue;

            var previous = tiers[index - 1];
            if (previous.MaxPopulation is not int previousMaximum
                || current.MinPopulation != previousMaximum + 1)
            {
                throw new InvalidDataException($"{DataPath}: Church population tiers must be continuous and non-overlapping.");
            }

            if (current.Tier < previous.Tier)
                throw new InvalidDataException($"{DataPath}: Church tier cannot fall as population rises.");
        }

        return new ChurchInstitutionRules(tiers);
    }

    public TownInstitutionInfo Resolve(int population)
    {
        var rule = _tiers.FirstOrDefault(candidate => candidate.Matches(Math.Max(0, population)))
            ?? _tiers[^1];

        return new TownInstitutionInfo(
            "church",
            "Church",
            rule.Tier,
            rule.DisplayName);
    }

    private sealed record TierRule(
        int MinPopulation,
        int? MaxPopulation,
        int Tier,
        string DisplayName)
    {
        public bool Matches(int population) =>
            population >= MinPopulation
            && (MaxPopulation is null || population <= MaxPopulation.Value);
    }

    private sealed class Root
    {
        public bool AlwaysPresent { get; set; }
        public List<TierRuleDto> TierByPopulation { get; set; } = [];
    }

    private sealed class TierRuleDto
    {
        public int? MinPopulation { get; set; }
        public int? MaxPopulation { get; set; }
        public int Tier { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
