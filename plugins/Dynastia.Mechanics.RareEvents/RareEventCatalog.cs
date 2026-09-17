using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

public sealed record RareEventDefinition(
    string EventId,
    string Name,
    string Pool,
    string Category,
    int StartYear,
    int? EndYear,
    double BaseWeight,
    int MinimumAge,
    int? MaximumAge,
    bool RequiresFinanceHousehold,
    decimal MinimumHouseholdWealth,
    bool RequiresEmployment,
    bool RequiresOwnedHouse,
    bool RequiresFarmland,
    bool RequiresCraft,
    double MinimumStress,
    string? StatId,
    string StatDirection,
    string TownPreference,
    SettlementClass MinimumSettlementClass,
    IReadOnlyList<string> RequiredOpportunityTags,
    IReadOnlyList<string> PreferredOpportunityTags,
    IReadOnlyList<string> PreferredCareerFamilies,
    string HandlerId)
{
    public bool IsAvailable(int year, int age) =>
        year >= StartYear
        && (EndYear is null || year <= EndYear.Value)
        && age >= MinimumAge
        && (MaximumAge is null || age <= MaximumAge.Value);
}

public sealed class RareEventCatalog
{
    private const string DataPath = "RareEvents/rare_events.csv";
    private static readonly HashSet<string> Pools = new(StringComparer.OrdinalIgnoreCase)
    {
        "Household", "Personal", "Special"
    };
    private static readonly HashSet<string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        "HouseholdDisaster", "HouseholdCrime", "Weather", "HouseholdLoss", "HouseholdFortune",
        "FarmingFortune", "FarmingLoss", "HealthCrisis", "Violence", "Accident", "Fortune",
        "FinancialLoss", "LegalMisfortune", "CraftLoss", "Opportunity", "CraftFortune", "MentalHealth"
    };
    private static readonly HashSet<string> Directions = new(StringComparer.OrdinalIgnoreCase)
    {
        "None", "High", "Low"
    };
    private static readonly HashSet<string> TownPreferences = new(StringComparer.OrdinalIgnoreCase)
    {
        "Universal", "Urban", "Rural"
    };
    private static readonly HashSet<string> Stats = new(StringComparer.OrdinalIgnoreCase)
    {
        "strength", "intellect", "appeal"
    };
    private static readonly HashSet<string> Handlers = new(StringComparer.OrdinalIgnoreCase)
    {
        "bespoke.house_fire", "bespoke.burglary", "bespoke.storm_flood",
        "bespoke.structural_accident", "bespoke.assault", "bespoke.mugging",
        "bespoke.workplace_accident", "bespoke.traffic_accident",
        "bespoke.lightning_strike", "bespoke.serious_fall", "bespoke.lottery_win",
        "bespoke.fraud", "bespoke.wrongful_arrest", "bespoke.scholarship",
        "bespoke.professional_recognition", "bespoke.exceptional_harvest",
        "bespoke.crop_failure", "bespoke.local_epidemic", "bespoke.craft_commission",
        "generic.wealth_loss", "generic.wealth_gain",
        "generic.health_damage_with_death_risk", "special.suicide"
    };

    private readonly IReadOnlyDictionary<string, RareEventDefinition> _byId;

    private RareEventCatalog(IReadOnlyList<RareEventDefinition> events)
    {
        Events = events;
        _byId = events.ToDictionary(item => item.EventId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<RareEventDefinition> Events { get; }

    public RareEventDefinition? Find(string eventId) =>
        _byId.TryGetValue(eventId, out var definition) ? definition : null;

    public IReadOnlyList<RareEventDefinition> GetPool(string pool) =>
        Events.Where(item => item.Pool.Equals(pool, StringComparison.OrdinalIgnoreCase)).ToList();

    public static RareEventCatalog Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var rows = Parse(data.ReadText(DataPath));
        Validate(rows);
        return new RareEventCatalog(rows);
    }

    private static IReadOnlyList<RareEventDefinition> Parse(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "EventId,Name,Pool,Category,StartYear,EndYear,BaseWeight,MinimumAge,MaximumAge,RequiresFinanceHousehold,MinimumHouseholdWealth,RequiresEmployment,RequiresOwnedHouse,RequiresFarmland,RequiresCraft,MinimumStress,StatId,StatDirection,TownPreference,MinimumSettlementClass,RequiredOpportunityTags,PreferredOpportunityTags,PreferredCareerFamilies,HandlerId";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{DataPath}: unexpected header or empty file.");

        var result = new List<RareEventDefinition>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 24)
                throw new InvalidDataException($"{DataPath} row {row}: expected 24 fields.");

            var stat = NormalizeOptional(fields[16]);
            if (stat == "-") stat = null;

            result.Add(new RareEventDefinition(
                fields[0].Trim(), fields[1].Trim(), fields[2].Trim(), fields[3].Trim(),
                ParseInt(fields[4], row, "StartYear"), ParseNullableInt(fields[5], row, "EndYear"),
                ParseDouble(fields[6], row, "BaseWeight"), ParseInt(fields[7], row, "MinimumAge"),
                ParseNullableInt(fields[8], row, "MaximumAge"), ParseBool(fields[9], row, "RequiresFinanceHousehold"),
                ParseDecimal(fields[10], row, "MinimumHouseholdWealth"), ParseBool(fields[11], row, "RequiresEmployment"),
                ParseBool(fields[12], row, "RequiresOwnedHouse"), ParseBool(fields[13], row, "RequiresFarmland"),
                ParseBool(fields[14], row, "RequiresCraft"), ParseDouble(fields[15], row, "MinimumStress"),
                stat, fields[17].Trim(), fields[18].Trim(), ParseSettlement(fields[19], row),
                ParseList(fields[20]), ParseList(fields[21]), ParseList(fields[22]), fields[23].Trim()));
        }
        return result;
    }

    private static void Validate(IReadOnlyList<RareEventDefinition> events)
    {
        if (events.Count == 0)
            throw new InvalidDataException($"{DataPath} contains no events.");
        if (events.Select(e => e.EventId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != events.Count)
            throw new InvalidDataException($"{DataPath} contains duplicate EventId values.");

        foreach (var item in events)
        {
            if (string.IsNullOrWhiteSpace(item.EventId) || string.IsNullOrWhiteSpace(item.Name))
                throw new InvalidDataException($"{DataPath}: EventId and Name are required.");
            if (!Pools.Contains(item.Pool))
                throw new InvalidDataException($"{item.EventId}: unknown pool '{item.Pool}'.");
            if (!Categories.Contains(item.Category))
                throw new InvalidDataException($"{item.EventId}: unknown category '{item.Category}'.");
            if (item.StartYear < GameCalendarConfiguration.GameStartYear || item.EndYear is int end && end < item.StartYear)
                throw new InvalidDataException($"{item.EventId}: invalid era.");
            if (!item.Pool.Equals("Special", StringComparison.OrdinalIgnoreCase) && item.BaseWeight <= 0)
                throw new InvalidDataException($"{item.EventId}: ordinary events require BaseWeight > 0.");
            if (item.Pool.Equals("Special", StringComparison.OrdinalIgnoreCase) && item.BaseWeight < 0)
                throw new InvalidDataException($"{item.EventId}: BaseWeight may not be negative.");
            if (item.MinimumAge < 0 || item.MaximumAge is int maxAge && maxAge < item.MinimumAge)
                throw new InvalidDataException($"{item.EventId}: invalid age range.");
            if (item.MinimumHouseholdWealth < 0 || item.MinimumStress < 0)
                throw new InvalidDataException($"{item.EventId}: wealth/stress requirements may not be negative.");
            if (item.StatId is not null && !Stats.Contains(item.StatId))
                throw new InvalidDataException($"{item.EventId}: unknown stat '{item.StatId}'.");
            if (!Directions.Contains(item.StatDirection))
                throw new InvalidDataException($"{item.EventId}: unknown StatDirection '{item.StatDirection}'.");
            if (item.StatId is null && !item.StatDirection.Equals("None", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"{item.EventId}: StatDirection requires StatId.");
            if (!TownPreferences.Contains(item.TownPreference))
                throw new InvalidDataException($"{item.EventId}: unknown TownPreference '{item.TownPreference}'.");
            if (!Handlers.Contains(item.HandlerId))
                throw new InvalidDataException($"{item.EventId}: unknown HandlerId '{item.HandlerId}'.");
        }
    }

    private static string? NormalizeOptional(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static IReadOnlyList<string> ParseList(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed == "-") return [];
        return trimmed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static int ParseInt(string value, int row, string field) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : throw new InvalidDataException($"{DataPath} row {row} {field}: invalid integer '{value}'.");

    private static int? ParseNullableInt(string value, int row, string field) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseInt(value, row, field);

    private static double ParseDouble(string value, int row, string field) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : throw new InvalidDataException($"{DataPath} row {row} {field}: invalid number '{value}'.");

    private static decimal ParseDecimal(string value, int row, string field) =>
        decimal.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : throw new InvalidDataException($"{DataPath} row {row} {field}: invalid decimal '{value}'.");

    private static bool ParseBool(string value, int row, string field) =>
        bool.TryParse(value.Trim(), out var parsed)
            ? parsed : throw new InvalidDataException($"{DataPath} row {row} {field}: invalid boolean '{value}'.");

    private static SettlementClass ParseSettlement(string value, int row) =>
        Enum.TryParse<SettlementClass>(value.Trim(), true, out var parsed)
            ? parsed : throw new InvalidDataException($"{DataPath} row {row} MinimumSettlementClass: invalid value '{value}'.");
}
