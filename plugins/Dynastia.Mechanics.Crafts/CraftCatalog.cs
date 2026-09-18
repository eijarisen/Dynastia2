using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

public sealed class CraftCatalog
{
    private const string DataPath = "Crafts/crafts.csv";
    private const string LinksPath = "Crafts/craft_career_links.csv";
    private const string VariantsPath = "Crafts/craft_variants.csv";
    private const string ContextPath = "Crafts/craft_context_weights.csv";
    private const string OpportunityTagsPath = "Towns/opportunity_tags.csv";

    private static readonly IReadOnlySet<string> AllowedStats =
        new HashSet<string>(["strength", "intellect"], StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> TownPreferences =
        new HashSet<string>(["Universal", "Rural", "Urban"], StringComparer.OrdinalIgnoreCase);

    // Compatibility-only aliases for legacy save/content IDs. Current craft
    // authoring metadata belongs in the validated CSV catalog below.
    private static readonly IReadOnlyDictionary<string, string> LegacyAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["blacksmithing"] = "metalworking",
            ["carpentry"] = "woodworking_carpentry",
            ["tailoring"] = "tailoring_sewing",
            ["weaving"] = "weaving_textiles",
            ["leatherworking"] = "leatherworking_shoemaking",
            ["baking"] = "baking_confectionery",
            ["butchery"] = "food_processing_preservation",
            ["cooking"] = "food_processing_preservation",
            ["brewing"] = "food_processing_preservation",
            ["milling"] = "food_processing_preservation",
            ["printing"] = "printing_bookbinding",
            ["bookbinding"] = "printing_bookbinding",
            ["pottery_glasswork"] = "pottery_ceramics",
            ["apothecary"] = "food_processing_preservation",
            ["wheelwrighting"] = "woodworking_carpentry",
            ["coopering"] = "woodworking_carpentry",
            ["locksmithing"] = "metalworking",
            ["boatbuilding"] = "shipwrighting",
            ["saddlery"] = "leatherworking_shoemaking",
            ["welding"] = "welding_metal_fabrication",
            ["appliance_repair"] = "mechanical_repair",
            ["computer_hardware"] = "computer_hardware_repair",
            ["programming"] = "computer_hardware_repair",
            ["network_administration"] = "computer_hardware_repair",
            ["web_design"] = "photography",
            ["digital_media_editing"] = "photography",
            ["cybersecurity"] = "computer_hardware_repair"
        };

    private readonly IReadOnlyList<CraftInfo> _all;
    private readonly IReadOnlyDictionary<string, CraftInfo> _byId;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CraftVariant>> _variants;

    private CraftCatalog(
        IReadOnlyList<CraftInfo> all,
        IReadOnlyDictionary<string, IReadOnlyList<CraftVariant>> variants)
    {
        _all = all;
        _byId = all.ToDictionary(craft => craft.Id, StringComparer.OrdinalIgnoreCase);
        _variants = variants;
    }

    public IReadOnlyList<CraftInfo> All => _all;

    public CraftInfo? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        if (_byId.TryGetValue(id, out var exact))
            return exact;
        return LegacyAliases.TryGetValue(id, out var canonical)
            && _byId.TryGetValue(canonical, out var migrated)
                ? migrated
                : null;
    }

    public string? CanonicalizeId(string? id) => Find(id)?.Id;

    public CraftInfo Present(CraftInfo craft, int year) =>
        craft with { Name = ResolveDisplayName(craft.Id, year) };

    public string ResolveDisplayName(string craftId, int year)
    {
        var craft = Find(craftId);
        if (craft is null)
            return craftId;

        if (_variants.TryGetValue(craft.Id, out var variants))
        {
            var variant = variants.FirstOrDefault(rule =>
                year >= rule.StartYear
                && (rule.EndYear is null || year <= rule.EndYear.Value));
            if (variant is not null)
                return variant.DisplayName;
        }

        return craft.Name;
    }

    public static CraftCatalog Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var baseRows = ParseCrafts(data.ReadText(DataPath));
        var links = ParseLinks(data.ReadText(LinksPath), baseRows.Select(row => row.Id).ToHashSet(StringComparer.OrdinalIgnoreCase));
        var opportunityTags = ParseOpportunityTags(data.ReadText(OpportunityTagsPath));

        var crafts = baseRows.Select(row =>
        {
            ValidateOpportunityTags(row, opportunityTags);
            var craftLinks = links.Where(link => link.CraftId.Equals(row.Id, StringComparison.OrdinalIgnoreCase)).ToList();
            var primary = craftLinks.Where(link => link.Relevance.Equals("Primary", StringComparison.OrdinalIgnoreCase))
                .Select(link => link.CareerId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var secondary = craftLinks.Where(link => link.Relevance.Equals("Secondary", StringComparison.OrdinalIgnoreCase))
                .Select(link => link.CareerId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (primary.Count == 0)
                throw CatalogValidation.Error(
                    LinksPath,
                    "at least one Primary career link for every craft",
                    item: row.Id,
                    field: "Relevance",
                    value: "<missing>");

            return new CraftInfo(
                row.Id,
                row.Name,
                row.StartYear,
                row.EndYear,
                row.MinimumLearningAge,
                row.BaseWeight,
                row.BaseSalary,
                row.PrimaryStat,
                row.SecondaryStat,
                row.TownPreference,
                row.MinimumSettlementClass,
                row.RequiredOpportunityTags,
                row.PreferredOpportunityTags,
                row.Emoji,
                row.SelfEmploymentTitle,
                primary,
                secondary);
        }).ToList();

        if (crafts.Count != 25)
            throw CatalogValidation.Error(
                DataPath,
                "exactly 25 target crafts",
                field: "RowCount",
                value: crafts.Count);

        ValidateContextRows(data.ReadText(ContextPath), crafts);
        var variants = ParseVariants(data.ReadText(VariantsPath), crafts);
        return new CraftCatalog(crafts, variants);
    }

    private static IReadOnlyList<BaseCraft> ParseCrafts(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "Id,Name,StartYear,EndYear,MinimumLearningAge,BaseWeight,BaseSalary,PrimaryStat,SecondaryStat,TownPreference,MinimumSettlementClass,RequiredOpportunityTags,PreferredOpportunityTags,Emoji,SelfEmploymentTitle";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw CatalogValidation.UnexpectedHeader(
                DataPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);

        var result = new List<BaseCraft>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 15)
                throw CatalogValidation.FieldCount(DataPath, row, fields.Length, 15);

            var id = fields[0].Trim();
            if (!ids.Add(id))
                throw CatalogValidation.Error(DataPath, "a unique craft ID", row, id, "Id", id);

            var startYear = CatalogValidation.ParseInt(DataPath, row, "StartYear", fields[2]);
            var endYear = string.IsNullOrWhiteSpace(fields[3]) ? (int?)null : CatalogValidation.ParseInt(DataPath, row, "EndYear", fields[3]);
            var minimumAge = CatalogValidation.ParseInt(DataPath, row, "MinimumLearningAge", fields[4]);
            var baseWeight = CatalogValidation.ParseDouble(DataPath, row, "BaseWeight", fields[5]);
            var baseSalary = CatalogValidation.ParseDecimal(DataPath, row, "BaseSalary", fields[6]);
            var primaryStat = fields[7].Trim();
            var secondaryStat = OptionalDash(fields[8]);
            var townPreference = fields[9].Trim();
            var selfEmploymentTitle = fields[14].Trim();

            var minimumSettlement = CatalogValidation.ParseEnum<SettlementClass>(
                DataPath, row, "MinimumSettlementClass", fields[10]);
            if (startYear < GameCalendarConfiguration.GameStartYear)
                throw CatalogValidation.Error(DataPath, $"a year at or after {GameCalendarConfiguration.GameStartYear}", row, id, "StartYear", startYear);
            if (endYear is int end && end < startYear)
                throw CatalogValidation.Error(DataPath, $"a year at or after StartYear ({startYear})", row, id, "EndYear", end);
            if (minimumAge < 0)
                throw CatalogValidation.Error(DataPath, "an age of at least 0", row, id, "MinimumLearningAge", minimumAge);
            if (baseWeight <= 0)
                throw CatalogValidation.Error(DataPath, "a number greater than 0", row, id, "BaseWeight", baseWeight);
            if (baseSalary < 400m || baseSalary > 800m)
                throw CatalogValidation.Error(DataPath, "a base salary from 400 through 800", row, id, "BaseSalary", baseSalary);
            if (!AllowedStats.Contains(primaryStat))
                throw CatalogValidation.Error(DataPath, $"one of: {string.Join(", ", AllowedStats)}", row, id, "PrimaryStat", primaryStat);
            if (secondaryStat is not null && !AllowedStats.Contains(secondaryStat))
                throw CatalogValidation.Error(DataPath, $"one of: {string.Join(", ", AllowedStats)}, or blank", row, id, "SecondaryStat", secondaryStat);
            if (secondaryStat?.Equals(primaryStat, StringComparison.OrdinalIgnoreCase) == true)
                throw CatalogValidation.Error(DataPath, "a stat different from PrimaryStat", row, id, "SecondaryStat", secondaryStat);
            if (!TownPreferences.Contains(townPreference))
                throw CatalogValidation.Error(DataPath, $"one of: {string.Join(", ", TownPreferences)}", row, id, "TownPreference", townPreference);
            if (string.IsNullOrWhiteSpace(selfEmploymentTitle))
                throw CatalogValidation.Error(DataPath, "a non-empty self-employment title", row, id, "SelfEmploymentTitle", selfEmploymentTitle);

            result.Add(new BaseCraft(
                id,
                fields[1].Trim(),
                startYear,
                endYear,
                minimumAge,
                baseWeight,
                baseSalary,
                primaryStat,
                secondaryStat,
                townPreference,
                minimumSettlement,
                ParseList(fields[11]),
                ParseList(fields[12]),
                fields[13].Trim(),
                selfEmploymentTitle));
        }

        return result;
    }

    private static IReadOnlyList<CraftCareerLink> ParseLinks(string text, IReadOnlySet<string> craftIds)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "CraftId,CareerId,Relevance";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw CatalogValidation.UnexpectedHeader(LinksPath, lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'), header);

        var result = new List<CraftCareerLink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 3)
                throw CatalogValidation.FieldCount(LinksPath, row, fields.Length, 3);
            var craftId = fields[0].Trim();
            var careerId = fields[1].Trim();
            var relevance = fields[2].Trim();
            if (!craftIds.Contains(craftId))
                throw CatalogValidation.Error(LinksPath, "a CraftId defined in Crafts/crafts.csv", row, craftId, "CraftId", craftId);
            if (!relevance.Equals("Primary", StringComparison.OrdinalIgnoreCase)
                && !relevance.Equals("Secondary", StringComparison.OrdinalIgnoreCase))
                throw CatalogValidation.Error(LinksPath, "Primary or Secondary", row, craftId, "Relevance", relevance);
            if (!seen.Add($"{craftId}|{careerId}"))
                throw CatalogValidation.Error(LinksPath, "a unique CraftId/CareerId pair", row, craftId, "CareerId", careerId);
            result.Add(new CraftCareerLink(craftId, careerId, relevance));
        }
        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<CraftVariant>> ParseVariants(
        string text,
        IReadOnlyList<CraftInfo> crafts)
    {
        var known = crafts.Select(craft => craft.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "CraftId,StartYear,EndYear,DisplayName";
        if (lines.Length < 1 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw CatalogValidation.UnexpectedHeader(VariantsPath, lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'), header);

        var rows = new List<CraftVariant>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 4)
                throw CatalogValidation.FieldCount(VariantsPath, row, fields.Length, 4);
            var craftId = fields[0].Trim();
            if (!known.Contains(craftId))
                throw CatalogValidation.Error(VariantsPath, "a CraftId defined in Crafts/crafts.csv", row, craftId, "CraftId", craftId);
            var start = CatalogValidation.ParseInt(VariantsPath, row, "StartYear", fields[1]);
            var end = string.IsNullOrWhiteSpace(fields[2]) ? (int?)null : CatalogValidation.ParseInt(VariantsPath, row, "EndYear", fields[2]);
            if (end is int endYear && endYear < start)
                throw CatalogValidation.Error(VariantsPath, $"a year at or after StartYear ({start})", row, craftId, "EndYear", endYear);
            rows.Add(new CraftVariant(row, craftId, start, end, fields[3].Trim()));
        }

        foreach (var group in rows.GroupBy(row => row.CraftId, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(row => row.StartYear).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var previous = ordered[i - 1];
                if (previous.EndYear is null || previous.EndYear.Value >= ordered[i].StartYear)
                    throw CatalogValidation.Error(
                        VariantsPath,
                        $"a StartYear after the previous variant ending at {previous.EndYear?.ToString() ?? "open-ended"}",
                        ordered[i].SourceRow,
                        group.Key,
                        "StartYear",
                        ordered[i].StartYear);
            }
        }

        return rows.GroupBy(row => row.CraftId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CraftVariant>)group.OrderBy(row => row.StartYear).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateContextRows(string text, IReadOnlyList<CraftInfo> crafts)
    {
        var byId = crafts.ToDictionary(craft => craft.Id, StringComparer.OrdinalIgnoreCase);
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "ItemId,StartYear,EndYear,Dimension,Value,WeightMultiplier";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw CatalogValidation.UnexpectedHeader(ContextPath, lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'), header);

        var ageProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 6)
                throw CatalogValidation.FieldCount(ContextPath, row, fields.Length, 6);
            var id = fields[0].Trim();
            if (!byId.TryGetValue(id, out var craft))
                throw CatalogValidation.Error(ContextPath, "a craft ID defined in Crafts/crafts.csv", row, id, "ItemId", id);
            var start = CatalogValidation.ParseInt(ContextPath, row, "StartYear", fields[1]);
            var end = string.IsNullOrWhiteSpace(fields[2]) ? (int?)null : CatalogValidation.ParseInt(ContextPath, row, "EndYear", fields[2]);
            if (start < craft.StartYear)
                throw CatalogValidation.Error(ContextPath, $"a year at or after craft StartYear ({craft.StartYear})", row, id, "StartYear", start);
            if (craft.EndYear is int craftEnd && (end ?? int.MaxValue) > craftEnd)
                throw CatalogValidation.Error(ContextPath, $"a year no later than craft EndYear ({craftEnd})", row, id, "EndYear", end);
            if (fields[3].Trim().Equals("AgeBand", StringComparison.OrdinalIgnoreCase))
                ageProfiles.Add(id);
        }

        var missing = crafts.FirstOrDefault(craft => !ageProfiles.Contains(craft.Id));
        if (missing is not null)
            throw CatalogValidation.Error(ContextPath, "at least one AgeBand row for every craft", item: missing.Id, field: "Dimension", value: "<missing>");
    }

    private static IReadOnlySet<string> ParseOpportunityTags(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').StartsWith("Tag,", StringComparison.Ordinal))
            throw CatalogValidation.Error(
                OpportunityTagsPath,
                "a header beginning with 'Tag,' and at least one data row",
                row: 1,
                field: "Header",
                value: lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'));
        return lines.Skip(1)
            .Select(line => line.Split(',')[0].Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateOpportunityTags(BaseCraft craft, IReadOnlySet<string> known)
    {
        var invalid = craft.RequiredOpportunityTags.Concat(craft.PreferredOpportunityTags)
            .FirstOrDefault(tag => !known.Contains(tag));
        if (invalid is not null)
            throw CatalogValidation.Error(
                DataPath,
                "an opportunity tag defined in Towns/opportunity_tags.csv",
                item: craft.Id,
                field: "RequiredOpportunityTags/PreferredOpportunityTags",
                value: invalid);
    }

    private static IReadOnlyList<string> ParseList(string value) =>
        value.Trim() == "-" || string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? OptionalDash(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim() == "-" ? null : value.Trim();

    private sealed record BaseCraft(
        string Id,
        string Name,
        int StartYear,
        int? EndYear,
        int MinimumLearningAge,
        double BaseWeight,
        decimal BaseSalary,
        string PrimaryStat,
        string? SecondaryStat,
        string TownPreference,
        SettlementClass MinimumSettlementClass,
        IReadOnlyList<string> RequiredOpportunityTags,
        IReadOnlyList<string> PreferredOpportunityTags,
        string Emoji,
        string SelfEmploymentTitle);

    private sealed record CraftCareerLink(string CraftId, string CareerId, string Relevance);
    private sealed record CraftVariant(int SourceRow, string CraftId, int StartYear, int? EndYear, string DisplayName);
}
