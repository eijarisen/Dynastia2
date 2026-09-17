using System.Globalization;
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

    private static readonly IReadOnlyDictionary<string, string> SelfEmploymentTitles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["metalworking"] = "Metalworker",
            ["woodworking_carpentry"] = "Carpenter",
            ["masonry"] = "Mason",
            ["pottery_ceramics"] = "Potter / Ceramist",
            ["glassworking"] = "Glassworker",
            ["tailoring_sewing"] = "Tailor",
            ["weaving_textiles"] = "Weaver",
            ["leatherworking_shoemaking"] = "Leatherworker / Shoemaker",
            ["jewellery_watchmaking"] = "Jeweller / Watchmaker",
            ["printing_bookbinding"] = "Printer / Bookbinder",
            ["baking_confectionery"] = "Baker / Confectioner",
            ["food_processing_preservation"] = "Food Processor",
            ["hairdressing_cosmetics"] = "Hairdresser",
            ["shipwrighting"] = "Shipwright",
            ["photography"] = "Photographer",
            ["plumbing"] = "Plumber",
            ["machining"] = "Machinist",
            ["mechanical_repair"] = "Mechanic",
            ["electrical_work"] = "Electrician",
            ["automotive_repair"] = "Auto Mechanic",
            ["welding_metal_fabrication"] = "Welder / Metal Fabricator",
            ["radio_electronics"] = "Electronics Technician",
            ["aircraft_maintenance"] = "Aircraft Mechanic",
            ["plastics_fabrication"] = "Plastics Fabricator",
            ["computer_hardware_repair"] = "Computer Technician"
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
                throw new InvalidDataException($"Craft '{row.Id}' must have at least one Primary career link.");

            return new CraftInfo(
                row.Id,
                row.Name,
                row.StartYear,
                row.EndYear,
                row.MinimumLearningAge,
                row.BaseWeight,
                row.PrimaryStat,
                row.SecondaryStat,
                row.TownPreference,
                row.MinimumSettlementClass,
                row.RequiredOpportunityTags,
                row.PreferredOpportunityTags,
                row.Emoji,
                SelfEmploymentTitles[row.Id],
                primary,
                secondary);
        }).ToList();

        if (crafts.Count != 25)
            throw new InvalidDataException($"{DataPath} must define exactly 25 target crafts; found {crafts.Count}.");

        ValidateContextRows(data.ReadText(ContextPath), crafts);
        var variants = ParseVariants(data.ReadText(VariantsPath), crafts);
        return new CraftCatalog(crafts, variants);
    }

    private static IReadOnlyList<BaseCraft> ParseCrafts(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "Id,Name,StartYear,EndYear,MinimumLearningAge,BaseWeight,PrimaryStat,SecondaryStat,TownPreference,MinimumSettlementClass,RequiredOpportunityTags,PreferredOpportunityTags,Emoji";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{DataPath} has an unexpected header.");

        var result = new List<BaseCraft>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 13)
                throw new InvalidDataException($"{DataPath} line {index + 1} has {fields.Length} fields; expected 13.");

            var id = fields[0].Trim();
            if (!ids.Add(id))
                throw new InvalidDataException($"{DataPath} contains duplicate craft ID '{id}'.");

            var startYear = ParseInt(fields[2], DataPath, index);
            var endYear = ParseOptionalInt(fields[3], DataPath, index);
            var minimumAge = ParseInt(fields[4], DataPath, index);
            var baseWeight = ParseDouble(fields[5], DataPath, index);
            var primaryStat = fields[6].Trim();
            var secondaryStat = OptionalDash(fields[7]);
            var townPreference = fields[8].Trim();

            if (!Enum.TryParse<SettlementClass>(fields[9].Trim(), true, out var minimumSettlement))
                throw new InvalidDataException($"{DataPath} line {index + 1} has invalid MinimumSettlementClass '{fields[9]}'.");
            if (startYear < GameCalendarConfiguration.GameStartYear || endYear is int end && end < startYear)
                throw new InvalidDataException($"Craft '{id}' has invalid historical availability.");
            if (minimumAge < 0)
                throw new InvalidDataException($"Craft '{id}' has a negative MinimumLearningAge.");
            if (baseWeight <= 0)
                throw new InvalidDataException($"Craft '{id}' has a non-positive BaseWeight.");
            if (!AllowedStats.Contains(primaryStat)
                || secondaryStat is not null && !AllowedStats.Contains(secondaryStat))
                throw new InvalidDataException($"Craft '{id}' has invalid aptitude stats.");
            if (secondaryStat?.Equals(primaryStat, StringComparison.OrdinalIgnoreCase) == true)
                throw new InvalidDataException($"Craft '{id}' repeats its PrimaryStat as SecondaryStat.");
            if (!TownPreferences.Contains(townPreference))
                throw new InvalidDataException($"Craft '{id}' has invalid TownPreference '{townPreference}'.");
            if (!SelfEmploymentTitles.ContainsKey(id))
                throw new InvalidDataException($"Craft '{id}' has no self-employment presentation title.");

            result.Add(new BaseCraft(
                id,
                fields[1].Trim(),
                startYear,
                endYear,
                minimumAge,
                baseWeight,
                primaryStat,
                secondaryStat,
                townPreference,
                minimumSettlement,
                ParseList(fields[10]),
                ParseList(fields[11]),
                fields[12].Trim()));
        }

        return result;
    }

    private static IReadOnlyList<CraftCareerLink> ParseLinks(string text, IReadOnlySet<string> craftIds)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "CraftId,CareerId,Relevance";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{LinksPath} has an unexpected header.");

        var result = new List<CraftCareerLink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 3)
                throw new InvalidDataException($"{LinksPath} row {index + 1}: expected 3 fields.");
            var craftId = fields[0].Trim();
            var careerId = fields[1].Trim();
            var relevance = fields[2].Trim();
            if (!craftIds.Contains(craftId))
                throw new InvalidDataException($"{LinksPath} row {index + 1}: unknown CraftId '{craftId}'.");
            if (!relevance.Equals("Primary", StringComparison.OrdinalIgnoreCase)
                && !relevance.Equals("Secondary", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"{LinksPath} row {index + 1}: invalid Relevance '{relevance}'.");
            if (!seen.Add($"{craftId}|{careerId}"))
                throw new InvalidDataException($"{LinksPath} contains duplicate link '{craftId}' -> '{careerId}'.");
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
            throw new InvalidDataException($"{VariantsPath} has an unexpected header.");

        var rows = new List<CraftVariant>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 4)
                throw new InvalidDataException($"{VariantsPath} row {index + 1}: expected 4 fields.");
            var craftId = fields[0].Trim();
            if (!known.Contains(craftId))
                throw new InvalidDataException($"{VariantsPath} row {index + 1}: unknown CraftId '{craftId}'.");
            var start = ParseInt(fields[1], VariantsPath, index);
            var end = ParseOptionalInt(fields[2], VariantsPath, index);
            if (end is int endYear && endYear < start)
                throw new InvalidDataException($"{VariantsPath} row {index + 1}: invalid range.");
            rows.Add(new CraftVariant(craftId, start, end, fields[3].Trim()));
        }

        foreach (var group in rows.GroupBy(row => row.CraftId, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(row => row.StartYear).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var previous = ordered[i - 1];
                if (previous.EndYear is null || previous.EndYear.Value >= ordered[i].StartYear)
                    throw new InvalidDataException($"{VariantsPath}: overlapping variants for '{group.Key}'.");
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
            throw new InvalidDataException($"{ContextPath} has an unexpected header.");

        var ageProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 6)
                throw new InvalidDataException($"{ContextPath} row {index + 1}: expected 6 fields.");
            var id = fields[0].Trim();
            if (!byId.TryGetValue(id, out var craft))
                throw new InvalidDataException($"{ContextPath} row {index + 1}: unknown craft '{id}'.");
            var start = ParseInt(fields[1], ContextPath, index);
            var end = ParseOptionalInt(fields[2], ContextPath, index);
            if (start < craft.StartYear
                || craft.EndYear is int craftEnd && (end ?? int.MaxValue) > craftEnd)
                throw new InvalidDataException($"{ContextPath} row {index + 1}: context years fall outside craft '{id}' availability.");
            if (fields[3].Trim().Equals("AgeBand", StringComparison.OrdinalIgnoreCase))
                ageProfiles.Add(id);
        }

        var missing = crafts.FirstOrDefault(craft => !ageProfiles.Contains(craft.Id));
        if (missing is not null)
            throw new InvalidDataException($"{ContextPath} is missing an AgeBand profile for craft '{missing.Id}'.");
    }

    private static IReadOnlySet<string> ParseOpportunityTags(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').StartsWith("Tag,", StringComparison.Ordinal))
            throw new InvalidDataException($"{OpportunityTagsPath} has an unexpected header.");
        return lines.Skip(1)
            .Select(line => line.Split(',')[0].Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateOpportunityTags(BaseCraft craft, IReadOnlySet<string> known)
    {
        var invalid = craft.RequiredOpportunityTags.Concat(craft.PreferredOpportunityTags)
            .FirstOrDefault(tag => !known.Contains(tag));
        if (invalid is not null)
            throw new InvalidDataException($"Craft '{craft.Id}' references unknown opportunity tag '{invalid}'.");
    }

    private static IReadOnlyList<string> ParseList(string value) =>
        value.Trim() == "-" || string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? OptionalDash(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim() == "-" ? null : value.Trim();

    private static int ParseInt(string value, string path, int row)
    {
        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"{path} row {row + 1}: invalid integer '{value}'.");
        return parsed;
    }

    private static int? ParseOptionalInt(string value, string path, int row) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseInt(value, path, row);

    private static double ParseDouble(string value, string path, int row)
    {
        if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"{path} row {row + 1}: invalid number '{value}'.");
        return parsed;
    }

    private sealed record BaseCraft(
        string Id,
        string Name,
        int StartYear,
        int? EndYear,
        int MinimumLearningAge,
        double BaseWeight,
        string PrimaryStat,
        string? SecondaryStat,
        string TownPreference,
        SettlementClass MinimumSettlementClass,
        IReadOnlyList<string> RequiredOpportunityTags,
        IReadOnlyList<string> PreferredOpportunityTags,
        string Emoji);

    private sealed record CraftCareerLink(string CraftId, string CareerId, string Relevance);
    private sealed record CraftVariant(string CraftId, int StartYear, int? EndYear, string DisplayName);
}
