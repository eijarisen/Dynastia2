using System.Globalization;
using System.Text;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

internal sealed class ArtisticWorkCatalog
{
    private const string TemplatesPath = "Heirlooms/artistic_work_templates.csv";
    private const string GenerationPath = "Heirlooms/artistic_work_generation_rules.csv";
    private const string WorkRulesPath = "Heirlooms/artistic_work_rules.json";
    private const string RoyaltyRulesPath = "Heirlooms/artistic_royalty_rules.json";

    private readonly IReadOnlyDictionary<int, ArtisticProductionRule> _productionByMastery;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<int, IReadOnlyList<ArtisticWorkTemplate>>> _templates;
    private readonly HashSet<string> _artisticCraftIds;

    private ArtisticWorkCatalog(
        IReadOnlyDictionary<int, ArtisticProductionRule> productionByMastery,
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, IReadOnlyList<ArtisticWorkTemplate>>> templates,
        IEnumerable<string> artisticCraftIds,
        int royaltyAfterDeathYears)
    {
        _productionByMastery = productionByMastery;
        _templates = templates;
        _artisticCraftIds = new HashSet<string>(artisticCraftIds, StringComparer.OrdinalIgnoreCase);
        RoyaltyAfterDeathYears = royaltyAfterDeathYears;
    }

    public int RoyaltyAfterDeathYears { get; }

    public bool IsArtisticCraft(string? craftId) =>
        !string.IsNullOrWhiteSpace(craftId)
        && _artisticCraftIds.Contains(craftId);

    public ArtisticProductionRule GetProductionRule(int masteryLevel) =>
        _productionByMastery.TryGetValue(masteryLevel, out var rule)
            ? rule
            : throw new InvalidOperationException($"Unsupported artistic Craft Mastery level {masteryLevel}.");

    public ArtisticWorkTemplate ChooseTemplate(
        string craftId,
        int masteryLevel,
        IGameRandom random)
    {
        if (!_templates.TryGetValue(craftId, out var byMastery)
            || !byMastery.TryGetValue(masteryLevel, out var templates)
            || templates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No artistic work template is configured for '{craftId}' at Mastery {masteryLevel}.");
        }

        return templates.Count == 1
            ? templates[0]
            : templates[random.NextInt(0, templates.Count - 1)];
    }

    public static ArtisticWorkCatalog Load(
        IGameDataService data,
        HeirloomCatalog heirlooms,
        IReadOnlyCollection<CraftInfo> crafts)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(heirlooms);

        var production = ParseGenerationRules(data.ReadText(GenerationPath));
        var templates = ParseTemplates(data.ReadText(TemplatesPath));
        var workRules = JsonDocument.Parse(data.ReadText(WorkRulesPath)).RootElement;
        var royaltyRules = JsonDocument.Parse(data.ReadText(RoyaltyRulesPath)).RootElement;

        ValidateWorkRules(workRules);

        var eligibleRoyaltyCrafts = royaltyRules
            .GetProperty("eligibleCraftIds")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var eligibleMinimumMastery = royaltyRules.GetProperty("eligibleMinimumMastery").GetInt32();
        var rates = royaltyRules
            .GetProperty("annualRatesByMastery")
            .EnumerateObject()
            .ToDictionary(
                item => int.Parse(item.Name, CultureInfo.InvariantCulture),
                item => item.Value.GetDecimal());
        var afterDeathYears = royaltyRules
            .GetProperty("duration")
            .GetProperty("afterAuthorDeathYears")
            .GetInt32();

        if (afterDeathYears != 70)
            throw new InvalidDataException($"{RoyaltyRulesPath}: afterAuthorDeathYears must be 70.");

        var knownCraftIds = crafts.Select(craft => craft.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var templateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var grouped = new Dictionary<string, Dictionary<int, List<ArtisticWorkTemplate>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in templates)
        {
            if (!knownCraftIds.Contains(template.CraftId))
                throw new InvalidDataException($"{TemplatesPath}: unknown CraftId '{template.CraftId}'.");

            _ = heirlooms.GetTemplate(template.TemplateId);
            if (!templateIds.Add(template.TemplateId))
                throw new InvalidDataException($"{TemplatesPath}: duplicate TemplateId '{template.TemplateId}'.");

            if (!production.TryGetValue(template.MasteryLevel, out var level))
                throw new InvalidDataException($"{TemplatesPath}: unsupported MasteryLevel {template.MasteryLevel}.");
            if (template.MinimumValue != level.MinimumValue || template.MaximumValue != level.MaximumValue)
            {
                throw new InvalidDataException(
                    $"{TemplatesPath}: '{template.TemplateId}' value band does not match Mastery {template.MasteryLevel} generation rules.");
            }

            var expectedRoyalty = eligibleRoyaltyCrafts.Contains(template.CraftId)
                && template.MasteryLevel >= eligibleMinimumMastery
                ? rates.GetValueOrDefault(template.MasteryLevel, 0m)
                : 0m;
            if (template.RoyaltyAnnualRate != expectedRoyalty)
            {
                throw new InvalidDataException(
                    $"{TemplatesPath}: '{template.TemplateId}' royalty rate does not match {RoyaltyRulesPath}.");
            }

            if (!grouped.TryGetValue(template.CraftId, out var byMastery))
            {
                byMastery = new Dictionary<int, List<ArtisticWorkTemplate>>();
                grouped[template.CraftId] = byMastery;
            }
            if (!byMastery.TryGetValue(template.MasteryLevel, out var list))
            {
                list = [];
                byMastery[template.MasteryLevel] = list;
            }
            list.Add(template);
        }

        foreach (var pair in grouped)
        {
            for (var mastery = 1; mastery <= 5; mastery++)
            {
                if (!pair.Value.TryGetValue(mastery, out var list) || list.Count == 0)
                {
                    throw new InvalidDataException(
                        $"{TemplatesPath}: Craft '{pair.Key}' has no template at Mastery {mastery}.");
                }
            }
        }

        if (grouped.Count != 4)
            throw new InvalidDataException($"{TemplatesPath}: expected exactly four artistic Crafts.");

        var readOnly = grouped.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<int, IReadOnlyList<ArtisticWorkTemplate>>)pair.Value.ToDictionary(
                level => level.Key,
                level => (IReadOnlyList<ArtisticWorkTemplate>)level.Value,
                EqualityComparer<int>.Default),
            StringComparer.OrdinalIgnoreCase);

        return new ArtisticWorkCatalog(
            production,
            readOnly,
            grouped.Keys,
            afterDeathYears);
    }

    private static Dictionary<int, ArtisticProductionRule> ParseGenerationRules(string text)
    {
        var rows = ParseCsv(text, GenerationPath);
        ValidateHeader(
            rows,
            GenerationPath,
            "MasteryLevel",
            "DisplayName",
            "AnnualProductionChance",
            "MinimumValue",
            "MaximumValue",
            "RenownGain",
            "ReputationGain");

        var result = new Dictionary<int, ArtisticProductionRule>();
        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index];
            var mastery = ParseInt(row[0], GenerationPath, index + 1);
            var rule = new ArtisticProductionRule(
                mastery,
                row[1].Trim(),
                ParseDouble(row[2], GenerationPath, index + 1),
                ParseInt(row[3], GenerationPath, index + 1),
                ParseInt(row[4], GenerationPath, index + 1));

            if (rule.AnnualProductionChance is < 0 or > 1
                || rule.MinimumValue <= 0
                || rule.MaximumValue < rule.MinimumValue)
            {
                throw new InvalidDataException($"{GenerationPath}: invalid row {index + 1}.");
            }

            if (!result.TryAdd(mastery, rule))
                throw new InvalidDataException($"{GenerationPath}: duplicate MasteryLevel {mastery}.");
        }

        if (!result.Keys.OrderBy(value => value).SequenceEqual([1, 2, 3, 4, 5]))
            throw new InvalidDataException($"{GenerationPath}: Mastery levels 1-5 are required.");

        return result;
    }

    private static IReadOnlyList<ArtisticWorkTemplate> ParseTemplates(string text)
    {
        var rows = ParseCsv(text, TemplatesPath);
        ValidateHeader(
            rows,
            TemplatesPath,
            "TemplateId",
            "CraftId",
            "MasteryLevel",
            "NamePattern",
            "Emoji",
            "MinimumValue",
            "MaximumValue",
            "RoyaltyAnnualRate");

        return rows.Skip(1)
            .Select((row, index) => new ArtisticWorkTemplate(
                row[0].Trim(),
                row[1].Trim(),
                ParseInt(row[2], TemplatesPath, index + 2),
                ParseInt(row[5], TemplatesPath, index + 2),
                ParseInt(row[6], TemplatesPath, index + 2),
                ParseDecimal(row[7], TemplatesPath, index + 2)))
            .ToList();
    }

    private static void ValidateWorkRules(JsonElement root)
    {
        if (!root.GetProperty("onlyWhileActiveSelfEmployment").GetBoolean()
            || !root.GetProperty("oneProductionRollPerArtistYear").GetBoolean())
        {
            throw new InvalidDataException($"{WorkRulesPath}: active self-employment and one-roll rules must be enabled.");
        }

        var guarantee = root.GetProperty("mastery5Guarantee");
        foreach (var property in new[]
        {
            "onFirstReachingMaster",
            "usesMasterValueBand",
            "countsAsThatYearProduction",
            "suppressRandomProductionRollThatYear",
            "replaceOrdinaryCraftMasterHeirloomForArtisticCrafts"
        })
        {
            if (!guarantee.GetProperty(property).GetBoolean())
                throw new InvalidDataException($"{WorkRulesPath}: mastery5Guarantee.{property} must be true.");
        }
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseCsv(string text, string path)
    {
        var rows = new List<IReadOnlyList<string>>();
        var currentRow = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else quoted = false;
                }
                else field.Append(ch);
                continue;
            }

            if (ch == '"') quoted = true;
            else if (ch == ',')
            {
                currentRow.Add(field.ToString().TrimStart('\uFEFF'));
                field.Clear();
            }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                currentRow.Add(field.ToString().TrimStart('\uFEFF'));
                field.Clear();
                if (currentRow.Any(value => value.Length > 0)) rows.Add(currentRow.ToList());
                currentRow.Clear();
            }
            else field.Append(ch);
        }

        if (field.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(field.ToString().TrimStart('\uFEFF'));
            if (currentRow.Any(value => value.Length > 0)) rows.Add(currentRow);
        }

        if (quoted)
            throw new InvalidDataException($"{path}: unterminated quoted field.");
        return rows;
    }

    private static void ValidateHeader(
        IReadOnlyList<IReadOnlyList<string>> rows,
        string path,
        params string[] expected)
    {
        if (rows.Count < 2 || !rows[0].SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException($"{path}: unexpected header or empty data.");
        foreach (var row in rows.Skip(1))
        {
            if (row.Count != expected.Length)
                throw new InvalidDataException($"{path}: row has {row.Count} fields, expected {expected.Length}.");
        }
    }

    private static int ParseInt(string raw, string path, int row) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"{path}: row {row} has invalid integer '{raw}'.");

    private static double ParseDouble(string raw, string path, int row) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"{path}: row {row} has invalid number '{raw}'.");

    private static decimal ParseDecimal(string raw, string path, int row) =>
        decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"{path}: row {row} has invalid decimal '{raw}'.");
}

internal sealed record ArtisticProductionRule(
    int MasteryLevel,
    string DisplayName,
    double AnnualProductionChance,
    int MinimumValue,
    int MaximumValue);

internal sealed record ArtisticWorkTemplate(
    string TemplateId,
    string CraftId,
    int MasteryLevel,
    int MinimumValue,
    int MaximumValue,
    decimal RoyaltyAnnualRate);
