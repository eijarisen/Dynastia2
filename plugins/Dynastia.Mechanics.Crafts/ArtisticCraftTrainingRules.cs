using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

public sealed class ArtisticCraftTrainingRules
{
    private const string AvailabilityPath = "Crafts/artistic_craft_availability.csv";
    private const string RulesPath = "Crafts/artistic_craft_rules.json";

    private readonly IReadOnlyDictionary<string, ArtisticCraftAvailability> _byCraftId;

    private ArtisticCraftTrainingRules(
        IReadOnlyDictionary<string, ArtisticCraftAvailability> byCraftId)
    {
        _byCraftId = byCraftId;
    }

    public IReadOnlyCollection<string> ArtisticCraftIds => _byCraftId.Keys.ToArray();

    public bool IsArtistic(string craftId) =>
        _byCraftId.ContainsKey(craftId);

    public int GetMinimumSchoolTier(string craftId) =>
        _byCraftId.TryGetValue(craftId, out var rule)
            ? rule.MinimumSchoolTier
            : 0;

    public bool MeetsTrainingAvailability(
        CraftInfo craft,
        int year,
        int age,
        LocationOpportunitySnapshot location,
        int schoolTier)
    {
        ArgumentNullException.ThrowIfNull(craft);
        ArgumentNullException.ThrowIfNull(location);

        var opportunityTags = location.RegionOpportunityTags
            .Concat(location.TownOpportunityTags)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!craft.MeetsHardAvailability(
                year,
                age,
                location.Town.SettlementClass,
                opportunityTags))
        {
            return false;
        }

        if (!_byCraftId.TryGetValue(craft.Id, out var artistic))
            return true;

        if (schoolTier < artistic.MinimumSchoolTier)
            return false;

        if (location.Town.SettlementClass is SettlementClass.City
            or SettlementClass.MajorCity)
        {
            return true;
        }

        return artistic.ArtsOpportunityTags.Any(opportunityTags.Contains);
    }

    public static ArtisticCraftTrainingRules Load(
        IGameDataService data,
        CraftCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(catalog);

        var rows = ParseAvailability(data.ReadText(AvailabilityPath));
        ValidateRulesDocument(data.ReadText(RulesPath), rows);

        foreach (var row in rows.Values)
        {
            if (catalog.Find(row.CraftId) is null)
            {
                throw CatalogValidation.Error(
                    AvailabilityPath,
                    "a CraftId defined in Crafts/crafts.csv",
                    item: row.CraftId,
                    field: "CraftId",
                    value: row.CraftId);
            }
        }

        return new ArtisticCraftTrainingRules(rows);
    }

    private static IReadOnlyDictionary<string, ArtisticCraftAvailability> ParseAvailability(
        string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header =
            "CraftId,MinimumSchoolTier,ArtSupportRule,ArtsOpportunityTags";

        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                AvailabilityPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var result = new Dictionary<string, ArtisticCraftAvailability>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var rowNumber = index + 1;
            if (fields.Length != 4)
                throw CatalogValidation.FieldCount(AvailabilityPath, rowNumber, fields.Length, 4);

            var craftId = fields[0].Trim();
            var minimumSchoolTier = CatalogValidation.ParseInt(
                AvailabilityPath,
                rowNumber,
                "MinimumSchoolTier",
                fields[1]);

            if (minimumSchoolTier is < 1 or > 5)
            {
                throw CatalogValidation.Error(
                    AvailabilityPath,
                    "a school tier from 1 through 5",
                    rowNumber,
                    craftId,
                    "MinimumSchoolTier",
                    minimumSchoolTier);
            }

            if (!fields[2].Trim().Equals(
                    "CityOrArtsOpportunity",
                    StringComparison.Ordinal))
            {
                throw CatalogValidation.Error(
                    AvailabilityPath,
                    "CityOrArtsOpportunity",
                    rowNumber,
                    craftId,
                    "ArtSupportRule",
                    fields[2].Trim());
            }

            var opportunityTags = fields[3]
                .Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
                .ToArray();

            if (opportunityTags.Length == 0)
            {
                throw CatalogValidation.Error(
                    AvailabilityPath,
                    "at least one arts opportunity tag",
                    rowNumber,
                    craftId,
                    "ArtsOpportunityTags",
                    fields[3]);
            }

            if (!result.TryAdd(
                    craftId,
                    new ArtisticCraftAvailability(
                        craftId,
                        minimumSchoolTier,
                        opportunityTags)))
            {
                throw CatalogValidation.Error(
                    AvailabilityPath,
                    "a unique CraftId",
                    rowNumber,
                    craftId,
                    "CraftId",
                    craftId);
            }
        }

        return result;
    }

    private static void ValidateRulesDocument(
        string text,
        IReadOnlyDictionary<string, ArtisticCraftAvailability> availability)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;

        if (root.GetProperty("craftCatalogBaseSalaryMinimum").GetInt32() != 100)
        {
            throw CatalogValidation.Error(
                RulesPath,
                "craftCatalogBaseSalaryMinimum = 100",
                field: "craftCatalogBaseSalaryMinimum",
                value: root.GetProperty("craftCatalogBaseSalaryMinimum").GetRawText());
        }

        var ordinaryRange = root.GetProperty("existingOrdinaryCraftSalaryRangeRemains")
            .EnumerateArray()
            .Select(item => item.GetInt32())
            .ToArray();
        if (ordinaryRange.Length != 2
            || ordinaryRange[0] != 400
            || ordinaryRange[1] != 800)
        {
            throw CatalogValidation.Error(
                RulesPath,
                "existing ordinary Craft salary range [400, 800]",
                field: "existingOrdinaryCraftSalaryRangeRemains",
                value: string.Join(";", ordinaryRange));
        }

        var ids = root.GetProperty("artisticCraftIds")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!ids.SetEquals(availability.Keys))
        {
            throw CatalogValidation.Error(
                RulesPath,
                "artisticCraftIds matching artistic_craft_availability.csv",
                field: "artisticCraftIds",
                value: string.Join(";", ids.OrderBy(id => id)));
        }

        var configuredMinimums =
            root.GetProperty("educationAvailability")
                .GetProperty("minimumSchoolTierByCraft");

        foreach (var pair in availability)
        {
            if (!configuredMinimums.TryGetProperty(pair.Key, out var configured)
                || configured.GetInt32() != pair.Value.MinimumSchoolTier)
            {
                throw CatalogValidation.Error(
                    RulesPath,
                    $"minimum school tier {pair.Value.MinimumSchoolTier}",
                    item: pair.Key,
                    field: $"minimumSchoolTierByCraft.{pair.Key}",
                    value: configured.ValueKind == JsonValueKind.Undefined
                        ? "<missing>"
                        : configured.GetRawText());
            }
        }
    }

    private sealed record ArtisticCraftAvailability(
        string CraftId,
        int MinimumSchoolTier,
        IReadOnlyList<string> ArtsOpportunityTags);
}
