using System.Globalization;
using System.Text;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

public sealed class HeirloomCatalog
{
    private const string CatalogPath = "Heirlooms/heirloom_catalog.csv";
    private const string RulesPath = "Heirlooms/heirloom_rules.json";
    private const string ArtisticCatalogPath = "Heirlooms/artistic_heirloom_catalog_append.csv";
    private const string NewsPath = "Heirlooms/heirloom_news_templates.csv";

    private readonly IReadOnlyDictionary<string, HeirloomTemplateDefinition> _templates;
    private readonly IReadOnlyDictionary<string, HeirloomNewsTemplate> _news;

    private HeirloomCatalog(
        IReadOnlyDictionary<string, HeirloomTemplateDefinition> templates,
        HeirloomRules rules,
        IReadOnlyDictionary<string, HeirloomNewsTemplate> news)
    {
        _templates = templates;
        Rules = rules;
        _news = news;
    }

    public HeirloomRules Rules { get; }

    public static HeirloomCatalog Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var templates = ParseCatalog(data.ReadText(CatalogPath), CatalogPath)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        foreach (var pair in ParseCatalog(data.ReadText(ArtisticCatalogPath), ArtisticCatalogPath))
        {
            if (!templates.TryAdd(pair.Key, pair.Value))
                throw new InvalidDataException($"{ArtisticCatalogPath}: duplicate TemplateId '{pair.Key}'.");
        }

        var news = ParseNews(data.ReadText(NewsPath));
        var rules = JsonSerializer.Deserialize<HeirloomRules>(
            data.ReadText(RulesPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"{RulesPath}: invalid root object.");

        Validate(templates, news, rules);
        return new HeirloomCatalog(templates, rules, news);
    }

    public HeirloomTemplateDefinition GetTemplate(string id) =>
        _templates.TryGetValue(id, out var item)
            ? item
            : throw new KeyNotFoundException($"Unknown heirloom template '{id}'.");

    public string BuildCreationNews(
        HeirloomTemplateDefinition template,
        string itemName,
        string? personName,
        IReadOnlyDictionary<string, string>? tokens)
    {
        if (!_news.TryGetValue(template.AcquisitionNewsKey, out var news))
            return $"{itemName} was preserved as a family heirloom.";

        var text = news.TextTemplate
            .Replace("{Item}", itemName, StringComparison.Ordinal)
            .Replace("{Person}", personName ?? "The family", StringComparison.Ordinal);

        if (tokens is not null)
        {
            foreach (var pair in tokens)
                text = text.Replace($"{{{pair.Key}}}", pair.Value, StringComparison.Ordinal);
        }

        // Later batches provide the remaining trigger-specific tokens. E1
        // still emits useful text when Create is called directly.
        return text.Contains('{')
            ? $"{itemName} was preserved as a family heirloom."
            : text;
    }

    private static IReadOnlyDictionary<string, HeirloomTemplateDefinition> ParseCatalog(
        string text,
        string path)
    {
        var rows = ParseCsv(text);
        if (rows.Count < 2)
            throw new InvalidDataException($"{path}: catalog is empty.");

        var header = rows[0];
        var expected = new[]
        {
            "TemplateId", "Category", "NamePattern", "Emoji", "BaseValue",
            "Significance", "StartYear", "EndYear", "AcquisitionNewsKey", "Notes"
        };
        if (!header.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException($"{path}: unexpected header.");

        var result = new Dictionary<string, HeirloomTemplateDefinition>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.Count != expected.Length)
                throw new InvalidDataException($"{path}: row {index + 1} has {row.Count} fields, expected {expected.Length}.");

            var template = new HeirloomTemplateDefinition(
                row[0].Trim(), row[1].Trim(), row[2].Trim(), row[3].Trim(),
                decimal.Parse(row[4], CultureInfo.InvariantCulture), row[5].Trim(),
                int.Parse(row[6], CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(row[7]) ? null : int.Parse(row[7], CultureInfo.InvariantCulture),
                row[8].Trim(), row[9].Trim());

            if (!result.TryAdd(template.TemplateId, template))
                throw new InvalidDataException($"{path}: duplicate TemplateId '{template.TemplateId}'.");
        }

        return result;
    }

    private static IReadOnlyDictionary<string, HeirloomNewsTemplate> ParseNews(string text)
    {
        var rows = ParseCsv(text);
        if (rows.Count < 2
            || !rows[0].SequenceEqual(new[] { "NewsKey", "Emoji", "TextTemplate" }, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{NewsPath}: unexpected header.");
        }

        var result = new Dictionary<string, HeirloomNewsTemplate>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.Count != 3)
                throw new InvalidDataException($"{NewsPath}: row {index + 1} has {row.Count} fields, expected 3.");
            result[row[0].Trim()] = new HeirloomNewsTemplate(row[0].Trim(), row[1].Trim(), row[2].Trim());
        }
        return result;
    }

    private static void Validate(
        IReadOnlyDictionary<string, HeirloomTemplateDefinition> templates,
        IReadOnlyDictionary<string, HeirloomNewsTemplate> news,
        HeirloomRules rules)
    {
        foreach (var template in templates.Values)
        {
            if (string.IsNullOrWhiteSpace(template.TemplateId)
                || string.IsNullOrWhiteSpace(template.Category)
                || string.IsNullOrWhiteSpace(template.NamePattern)
                || string.IsNullOrWhiteSpace(template.Emoji))
            {
                throw new InvalidDataException($"{CatalogPath}: template fields cannot be blank.");
            }

            if (template.BaseValue <= 0m)
                throw new InvalidDataException($"{CatalogPath}: '{template.TemplateId}' has non-positive BaseValue.");
            if (!news.ContainsKey(template.AcquisitionNewsKey))
                throw new InvalidDataException($"{CatalogPath}: '{template.TemplateId}' references unknown news key '{template.AcquisitionNewsKey}'.");
        }

        if (rules.Sale.SaleValueMultiplier <= 0m || rules.Sale.SaleValueMultiplier > 1m)
            throw new InvalidDataException($"{RulesPath}: invalid sale multiplier.");
        if (rules.Creation.ValueVarianceMultiplierMin <= 0m
            || rules.Creation.ValueVarianceMultiplierMax < rules.Creation.ValueVarianceMultiplierMin)
        {
            throw new InvalidDataException($"{RulesPath}: invalid creation value variance.");
        }
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseCsv(string text)
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
            else if (ch == '\r' || ch == '\n')
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

        return rows;
    }
}

public sealed record HeirloomTemplateDefinition(
    string TemplateId,
    string Category,
    string NamePattern,
    string Emoji,
    decimal BaseValue,
    string Significance,
    int StartYear,
    int? EndYear,
    string AcquisitionNewsKey,
    string Notes);

public sealed record HeirloomNewsTemplate(string NewsKey, string Emoji, string TextTemplate);

public sealed class HeirloomRules
{
    public HeirloomSaleRules Sale { get; set; } = new();
    public HeirloomCreationRules Creation { get; set; } = new();
}

public sealed class HeirloomSaleRules
{
    public decimal SaleValueMultiplier { get; set; } = 0.8m;
}

public sealed class HeirloomCreationRules
{
    public decimal ValueVarianceMultiplierMin { get; set; } = 0.9m;
    public decimal ValueVarianceMultiplierMax { get; set; } = 1.1m;
}
