using System.Globalization;
using System.Text;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

internal sealed class HeirloomAchievementCatalog
{
    private const string AcademicPath = "Heirlooms/academic_heirloom_pools.csv";
    private const string CareerPath = "Heirlooms/career_family_heirlooms.csv";
    private const string CraftPath = "Heirlooms/craft_master_heirlooms.csv";
    private const string DeathPath = "Heirlooms/death_heirloom_rules.json";
    private const string WealthPath = "Heirlooms/wealth_heirloom_tiers.csv";

    private HeirloomAchievementCatalog(
        IReadOnlyList<AcademicHeirloomPool> academicPools,
        IReadOnlyDictionary<string, string> careerTemplates,
        IReadOnlyDictionary<string, string> craftTemplates,
        DeathHeirloomRules deathRules,
        IReadOnlyList<WealthHeirloomTier> wealthTiers)
    {
        AcademicPools = academicPools;
        CareerTemplates = careerTemplates;
        CraftTemplates = craftTemplates;
        DeathRules = deathRules;
        WealthTiers = wealthTiers;
    }

    public IReadOnlyList<AcademicHeirloomPool> AcademicPools { get; }
    public IReadOnlyDictionary<string, string> CareerTemplates { get; }
    public IReadOnlyDictionary<string, string> CraftTemplates { get; }
    public DeathHeirloomRules DeathRules { get; }
    public IReadOnlyList<WealthHeirloomTier> WealthTiers { get; }

    public static HeirloomAchievementCatalog Load(
        IGameDataService data,
        HeirloomCatalog heirlooms,
        IReadOnlyCollection<string> knownCareerFamilies,
        IReadOnlyCollection<CraftInfo> knownCrafts)
    {
        var academic = ParseAcademic(data.ReadText(AcademicPath));
        var career = ParseMapping(data.ReadText(CareerPath), CareerPath, "CareerFamily", "TemplateId");
        var craft = ParseMapping(data.ReadText(CraftPath), CraftPath, "CraftId", "TemplateId");
        var death = JsonSerializer.Deserialize<DeathHeirloomRules>(
            data.ReadText(DeathPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"{DeathPath}: invalid root object.");
        var wealth = ParseWealth(data.ReadText(WealthPath));

        ValidateTemplates(heirlooms, academic.SelectMany(pool => pool.TemplateIds), AcademicPath);
        ValidateTemplates(heirlooms, career.Values, CareerPath);
        ValidateTemplates(heirlooms, craft.Values, CraftPath);
        ValidateTemplates(heirlooms, death.LargeFamily.TemplateIds, DeathPath);
        ValidateTemplates(heirlooms, death.Longevity.TemplateIds, DeathPath);
        ValidateTemplates(heirlooms, wealth.SelectMany(tier => tier.TemplateIds), WealthPath);

        var families = knownCareerFamilies.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownFamily = career.Keys.FirstOrDefault(key => !families.Contains(key));
        if (unknownFamily is not null)
            throw new InvalidDataException($"{CareerPath}: unknown CareerFamily '{unknownFamily}'.");

        var craftIds = knownCrafts.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownCraft = craft.Keys.FirstOrDefault(key => !craftIds.Contains(key));
        if (unknownCraft is not null)
            throw new InvalidDataException($"{CraftPath}: unknown CraftId '{unknownCraft}'.");

        if (death.LargeFamily.MinimumLivingChildren < 1 || death.LargeFamily.TemplateIds.Count == 0)
            throw new InvalidDataException($"{DeathPath}: invalid large-family rule.");
        if (death.Longevity.MinimumAgeAtDeath < 1 || death.Longevity.TemplateIds.Count == 0)
            throw new InvalidDataException($"{DeathPath}: invalid longevity rule.");

        var duplicateTier = wealth
            .GroupBy(tier => tier.TierId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateTier is not null)
            throw new InvalidDataException($"{WealthPath}: duplicate TierId '{duplicateTier.Key}'.");

        foreach (var tier in wealth)
        {
            if (tier.MinimumHouseholdWealth < 100000m)
                throw new InvalidDataException($"{WealthPath}: tier '{tier.TierId}' is below 100,000 zł.");
            if (tier.GenerationChanceOnFirstCrossing is < 0 or > 1)
                throw new InvalidDataException($"{WealthPath}: tier '{tier.TierId}' has an invalid chance.");
            if (tier.TemplateIds.Count == 0)
                throw new InvalidDataException($"{WealthPath}: tier '{tier.TierId}' has no templates.");
        }

        return new HeirloomAchievementCatalog(academic, career, craft, death, wealth);
    }

    public AcademicHeirloomPool? GetAcademicPool(int year) =>
        AcademicPools.FirstOrDefault(pool =>
            year >= pool.StartYear
            && (pool.EndYear is null || year <= pool.EndYear.Value));

    private static IReadOnlyList<AcademicHeirloomPool> ParseAcademic(string text)
    {
        var rows = ParseCsv(text, AcademicPath);
        ValidateHeader(rows, AcademicPath, "StartYear", "EndYear", "TemplateIds");
        return rows.Skip(1)
            .Select((row, index) => new AcademicHeirloomPool(
                ParseInt(row[0], AcademicPath, index + 2, "StartYear"),
                string.IsNullOrWhiteSpace(row[1]) ? null : ParseInt(row[1], AcademicPath, index + 2, "EndYear"),
                SplitIds(row[2], AcademicPath, index + 2)))
            .ToList();
    }

    private static IReadOnlyDictionary<string, string> ParseMapping(
        string text,
        string path,
        string keyHeader,
        string valueHeader)
    {
        var rows = ParseCsv(text, path);
        ValidateHeader(rows, path, keyHeader, valueHeader);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < rows.Count; index++)
        {
            var key = rows[index][0].Trim();
            var value = rows[index][1].Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException($"{path}: row {index + 1} contains a blank key/value.");
            if (!result.TryAdd(key, value))
                throw new InvalidDataException($"{path}: duplicate '{key}'.");
        }
        return result;
    }

    private static IReadOnlyList<WealthHeirloomTier> ParseWealth(string text)
    {
        var rows = ParseCsv(text, WealthPath);
        ValidateHeader(rows, WealthPath,
            "TierId", "MinimumHouseholdWealth", "GenerationChanceOnFirstCrossing", "TemplateIds");
        return rows.Skip(1)
            .Select((row, index) => new WealthHeirloomTier(
                row[0].Trim(),
                ParseDecimal(row[1], WealthPath, index + 2, "MinimumHouseholdWealth"),
                ParseDouble(row[2], WealthPath, index + 2, "GenerationChanceOnFirstCrossing"),
                SplitIds(row[3], WealthPath, index + 2)))
            .OrderBy(tier => tier.MinimumHouseholdWealth)
            .ToList();
    }

    private static void ValidateTemplates(
        HeirloomCatalog catalog,
        IEnumerable<string> templateIds,
        string path)
    {
        foreach (var templateId in templateIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                _ = catalog.GetTemplate(templateId);
            }
            catch (KeyNotFoundException exception)
            {
                throw new InvalidDataException($"{path}: unknown TemplateId '{templateId}'.", exception);
            }
        }
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

    private static IReadOnlyList<string> SplitIds(string raw, string path, int row)
    {
        var result = raw.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (result.Length == 0)
            throw new InvalidDataException($"{path}: row {row} has no TemplateIds.");
        return result;
    }

    private static int ParseInt(string raw, string path, int row, string field) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"{path}: row {row} has invalid {field} '{raw}'.");

    private static decimal ParseDecimal(string raw, string path, int row, string field) =>
        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"{path}: row {row} has invalid {field} '{raw}'.");

    private static double ParseDouble(string raw, string path, int row, string field) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"{path}: row {row} has invalid {field} '{raw}'.");

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
}

internal sealed record AcademicHeirloomPool(
    int StartYear,
    int? EndYear,
    IReadOnlyList<string> TemplateIds);

internal sealed record WealthHeirloomTier(
    string TierId,
    decimal MinimumHouseholdWealth,
    double GenerationChanceOnFirstCrossing,
    IReadOnlyList<string> TemplateIds);

internal sealed class DeathHeirloomRules
{
    public DeathHeirloomRule LargeFamily { get; set; } = new();
    public DeathHeirloomRule Longevity { get; set; } = new();
    public bool MayGenerateBothOnSameDeath { get; set; } = true;
}

internal sealed class DeathHeirloomRule
{
    public int MinimumLivingChildren { get; set; }
    public int MinimumAgeAtDeath { get; set; }
    public bool Guaranteed { get; set; }
    public List<string> TemplateIds { get; set; } = [];
}
