using System.Globalization;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Historical;

internal sealed class HistoricalEventCatalog
{
    private const string EventsPath = "HistoricalEvents/historical_events.csv";
    private const string ScopesPath = "HistoricalEvents/historical_event_scopes.json";
    private const string FiltersPath = "HistoricalEvents/historical_event_target_filters.json";
    private const string ProfilesPath = "HistoricalEvents/historical_effect_profiles.json";
    private const string RoutesPath = "HistoricalEvents/historical_migration_routes.json";
    private const string ModifiersPath = "HistoricalEvents/historical_candidate_modifiers.csv";
    private const string PeriodsPath = "HistoricalEvents/historical_periods.csv";
    private const string SourcesPath = "HistoricalEvents/historical_event_sources.json";

    public IReadOnlyList<HistoricalScheduledEvent> Events { get; }
    public IReadOnlyDictionary<string, HistoricalScopeDefinition> Scopes { get; }
    public IReadOnlyDictionary<string, HistoricalTargetFilterDefinition> Filters { get; }
    public IReadOnlyDictionary<string, HistoricalEffectProfile> Profiles { get; }
    public IReadOnlyDictionary<string, HistoricalMigrationRoute> Routes { get; }
    public IReadOnlyList<HistoricalCandidateModifier> CandidateModifiers { get; }
    public IReadOnlyList<HistoricalPeriodDefinition> Periods { get; }

    private HistoricalEventCatalog(
        IReadOnlyList<HistoricalScheduledEvent> events,
        IReadOnlyDictionary<string, HistoricalScopeDefinition> scopes,
        IReadOnlyDictionary<string, HistoricalTargetFilterDefinition> filters,
        IReadOnlyDictionary<string, HistoricalEffectProfile> profiles,
        IReadOnlyDictionary<string, HistoricalMigrationRoute> routes,
        IReadOnlyList<HistoricalCandidateModifier> candidateModifiers,
        IReadOnlyList<HistoricalPeriodDefinition> periods)
    {
        Events = events;
        Scopes = scopes;
        Filters = filters;
        Profiles = profiles;
        Routes = routes;
        CandidateModifiers = candidateModifiers;
        Periods = periods;
    }

    public static HistoricalEventCatalog Load(
        IGameDataService data,
        IHistoricalTownCatalog towns,
        INationalityService nationalities)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(towns);
        ArgumentNullException.ThrowIfNull(nationalities);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var sourceDescriptions = ParseSourceDescriptions(data.ReadText(SourcesPath));
        var events = ParseEvents(data.ReadText(EventsPath), sourceDescriptions);
        var scopes = CatalogValidation.DeserializeJson<List<HistoricalScopeDefinition>>(
                data,
                ScopesPath,
                options)
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var filters = CatalogValidation.DeserializeJson<List<HistoricalTargetFilterDefinition>>(
                data,
                FiltersPath,
                options)
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var profiles = CatalogValidation.DeserializeJson<List<HistoricalEffectProfile>>(
                data,
                ProfilesPath,
                options)
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var routes = CatalogValidation.DeserializeJson<List<HistoricalMigrationRoute>>(
                data,
                RoutesPath,
                options)
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var modifiers = ParseCandidateModifiers(data.ReadText(ModifiersPath));
        var periods = ParsePeriods(data.ReadText(PeriodsPath));

        Validate(events, scopes, filters, profiles, routes, modifiers, periods, towns, nationalities);

        return new HistoricalEventCatalog(
            events,
            scopes,
            filters,
            profiles,
            routes,
            modifiers,
            periods);
    }

    private static List<HistoricalScheduledEvent> ParseEvents(
        string text,
        IReadOnlyDictionary<string, string> sourceDescriptions)
    {
        var rows = ParseCsv(text, EventsPath);
        var result = new List<HistoricalScheduledEvent>();
        foreach (var row in rows)
        {
            var sourceGroupId = Optional(row, "SourceGroupId");
            var eventId = Require(row, "Id", EventsPath);
            var description = sourceGroupId is { Length: > 0 }
                && sourceDescriptions.TryGetValue(sourceGroupId, out var sourceDescription)
                    ? GetEventSpecificDescription(eventId, sourceGroupId, sourceDescription)
                    : Optional(row, "Notes") ?? string.Empty;

            result.Add(new HistoricalScheduledEvent(
                eventId,
                Require(row, "DisplayName", EventsPath),
                ParseInt(row, "StartYear", EventsPath),
                ParseInt(row, "EndYear", EventsPath),
                Require(row, "TriggerMode", EventsPath),
                Require(row, "ScopeId", EventsPath),
                Require(row, "TargetFilterId", EventsPath),
                Require(row, "EffectProfileId", EventsPath),
                Optional(row, "MigrationRouteId"),
                Optional(row, "ExclusiveGroup"),
                ParseInt(row, "Priority", EventsPath),
                Optional(row, "PlayerImpactPolicy") ?? "normal",
                ParseBool01(row, "ChronicleAtStart", EventsPath),
                ParseBool01(row, "HouseholdNews", EventsPath),
                sourceGroupId,
                description));
        }

        return result;
    }

    private static string GetEventSpecificDescription(
        string eventId,
        string sourceGroupId,
        string defaultDescription)
    {
        if (!sourceGroupId.Equals("partitions", StringComparison.OrdinalIgnoreCase))
            return defaultDescription;

        return eventId.ToLowerInvariant() switch
        {
            "first_partition" =>
                "In 1772, Russia, Prussia and Austria seized large territories from the Polish–Lithuanian Commonwealth in the First Partition, placing many towns and families under new rule.",
            "second_partition" =>
                "In 1793, Russia and Prussia carried out the Second Partition, taking further Commonwealth territory and deepening political and economic disruption.",
            "third_partition" =>
                "In 1795, Russia, Prussia and Austria carried out the Third Partition, ending the Polish–Lithuanian Commonwealth as an independent state.",
            _ => defaultDescription
        };
    }

    private static Dictionary<string, string> ParseSourceDescriptions(string text)
    {
        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw CatalogValidation.Error(SourcesPath, "a JSON object keyed by source group ID");

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!property.Value.TryGetProperty("note", out var noteElement)
                || noteElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(noteElement.GetString()))
            {
                throw CatalogValidation.Error(
                    SourcesPath,
                    "a non-empty note for every source group",
                    item: property.Name,
                    field: "note");
            }

            result[property.Name] = ToPlayerFacingSourceDescription(
                property.Name,
                noteElement.GetString()!.Trim());
        }

        return result;
    }

    private static string ToPlayerFacingSourceDescription(
        string sourceGroupId,
        string note)
    {
        return sourceGroupId.ToLowerInvariant() switch
        {
            "partitions" =>
                "The partitions of 1772, 1793 and 1795 transferred large parts of the Commonwealth to neighboring powers, changing borders and administration for millions of residents.",
            "ww2_occupation" =>
                "German and Soviet occupation brought severe shortages, forced labor, repression and widespread disruption across occupied Polish lands.",
            "volhynia" =>
                "Mass anti-Polish violence in Volhynia and Eastern Galicia in 1943–44 caused large-scale killing and flight, while retaliatory Polish attacks also killed Ukrainian civilians.",
            "market_transition" =>
                "The transition from a centrally planned economy brought rapid restructuring, business closures, unemployment and major changes in household finances.",
            "ukraine_refugees" =>
                "Following Russia's full-scale invasion of Ukraine, Poland received a very large refugee influx, especially women and children fleeing the war.",
            _ => note
        };
    }

    private static List<HistoricalCandidateModifier> ParseCandidateModifiers(string text)
    {
        var rows = ParseCsv(text, ModifiersPath);
        return rows.Select(row => new HistoricalCandidateModifier(
                Require(row, "Id", ModifiersPath),
                Require(row, "EventId", ModifiersPath),
                ParseInt(row, "StartYear", ModifiersPath),
                ParseInt(row, "EndYear", ModifiersPath),
                Require(row, "ScopeId", ModifiersPath),
                Require(row, "NationalityId", ModifiersPath),
                ParseDouble(row, "WeightMultiplier", ModifiersPath),
                ParseInt(row, "Priority", ModifiersPath)))
            .ToList();
    }

    private static List<HistoricalPeriodDefinition> ParsePeriods(string text)
    {
        var rows = ParseCsv(text, PeriodsPath);
        return rows.Select(row => new HistoricalPeriodDefinition(
                Require(row, "Id", PeriodsPath),
                ParseInt(row, "StartYear", PeriodsPath),
                ParseInt(row, "EndYear", PeriodsPath),
                Require(row, "DisplayName", PeriodsPath)))
            .ToList();
    }

    private static void Validate(
        IReadOnlyList<HistoricalScheduledEvent> events,
        IReadOnlyDictionary<string, HistoricalScopeDefinition> scopes,
        IReadOnlyDictionary<string, HistoricalTargetFilterDefinition> filters,
        IReadOnlyDictionary<string, HistoricalEffectProfile> profiles,
        IReadOnlyDictionary<string, HistoricalMigrationRoute> routes,
        IReadOnlyList<HistoricalCandidateModifier> modifiers,
        IReadOnlyList<HistoricalPeriodDefinition> periods,
        IHistoricalTownCatalog towns,
        INationalityService nationalities)
    {
        if (events.Count == 0)
            throw CatalogValidation.Error(EventsPath, "at least one historical event");

        var knownTriggerModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "one_shot",
            "annual",
            "annual_until_impacted",
            "annual_until_ineligible",
            "modifier"
        };
        var knownScopeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "all_places",
            "regions",
            "places",
            "polity_snapshot",
            "current_event_polity",
            "polity_change",
            "intersection",
            "union"
        };

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in events)
        {
            if (!ids.Add(item.Id))
                throw CatalogValidation.Error(EventsPath, "unique event IDs", item: item.Id);
            if (item.StartYear < 1700 || item.EndYear > 2026 || item.EndYear < item.StartYear)
                throw CatalogValidation.Error(EventsPath, "a valid 1700-2026 year range", item: item.Id, field: "Years", value: $"{item.StartYear}-{item.EndYear}");
            if (!knownTriggerModes.Contains(item.TriggerMode))
                throw CatalogValidation.Error(EventsPath, "a supported trigger mode", item: item.Id, field: "TriggerMode", value: item.TriggerMode);
            if (!scopes.ContainsKey(item.ScopeId))
                throw CatalogValidation.Error(EventsPath, "a known ScopeId", item: item.Id, field: "ScopeId", value: item.ScopeId);
            if (!filters.ContainsKey(item.TargetFilterId))
                throw CatalogValidation.Error(EventsPath, "a known TargetFilterId", item: item.Id, field: "TargetFilterId", value: item.TargetFilterId);
            if (!profiles.ContainsKey(item.EffectProfileId))
                throw CatalogValidation.Error(EventsPath, "a known EffectProfileId", item: item.Id, field: "EffectProfileId", value: item.EffectProfileId);
            if (item.MigrationRouteId is not null && !routes.ContainsKey(item.MigrationRouteId))
                throw CatalogValidation.Error(EventsPath, "a known MigrationRouteId", item: item.Id, field: "MigrationRouteId", value: item.MigrationRouteId);
            if (item.Priority < 0)
                throw CatalogValidation.Error(EventsPath, "a non-negative priority", item: item.Id, field: "Priority", value: item.Priority);
        }

        var knownRegions = towns.Regions.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownPlaces = towns.PermanentPlaceIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownPolities = towns.Polities.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var scope in scopes.Values)
        {
            if (!knownScopeTypes.Contains(scope.Type))
                throw CatalogValidation.Error(ScopesPath, "a supported scope type", item: scope.Id, field: "type", value: scope.Type);

            foreach (var regionId in scope.RegionMultipliers?.Keys ?? Enumerable.Empty<string>())
            {
                if (!knownRegions.Contains(regionId))
                    throw CatalogValidation.Error(ScopesPath, "a Town RegionId", item: scope.Id, field: "regionMultipliers", value: regionId);
            }

            foreach (var placeId in scope.PlaceMultipliers?.Keys ?? Enumerable.Empty<string>())
            {
                if (!knownPlaces.Contains(placeId))
                    throw CatalogValidation.Error(ScopesPath, "a Town PlaceId", item: scope.Id, field: "placeMultipliers", value: placeId);
            }

            foreach (var polityId in (scope.PolityIds ?? []).Concat(scope.FromPolityIds ?? []).Concat(scope.ToPolityIds ?? []))
            {
                if (!knownPolities.Contains(polityId))
                    throw CatalogValidation.Error(ScopesPath, "a known polity", item: scope.Id, field: "polityId", value: polityId);
            }

            foreach (var nested in scope.ScopeIds ?? [])
            {
                if (!scopes.ContainsKey(nested))
                    throw CatalogValidation.Error(ScopesPath, "a known nested scope", item: scope.Id, field: "scopeIds", value: nested);
            }
        }

        foreach (var filter in filters.Values)
        {
            foreach (var nationalityId in filter.NationalityIds ?? [])
                _ = nationalities.GetDisplayName(nationalityId);

            if (filter.MinAge is int minAge && minAge < 0)
                throw CatalogValidation.Error(FiltersPath, "a non-negative minimum age", item: filter.Id, field: "minAge", value: minAge);
            if (filter.MaxAge is int maxAge && maxAge < 0)
                throw CatalogValidation.Error(FiltersPath, "a non-negative maximum age", item: filter.Id, field: "maxAge", value: maxAge);
            if (filter.MinAge is int minimum && filter.MaxAge is int maximum && maximum < minimum)
                throw CatalogValidation.Error(FiltersPath, "maxAge at or above minAge", item: filter.Id, field: "maxAge", value: maximum);
        }

        foreach (var profile in profiles.Values)
        {
            ValidateProbability(profile.HouseholdExposureChance, ProfilesPath, profile.Id, "householdExposureChance");
            ValidateProbability(profile.JobLossChance, ProfilesPath, profile.Id, "jobLossChance");
            ValidateProbability(profile.ImprisonmentChance, ProfilesPath, profile.Id, "imprisonmentChance");
            ValidateProbability(profile.PropertyConfiscationChance, ProfilesPath, profile.Id, "propertyConfiscationChance");
            ValidateProbability(profile.HouseRepairCostChance, ProfilesPath, profile.Id, "houseRepairCostChance");
            ValidateProbability(profile.InternalRelocationChance, ProfilesPath, profile.Id, "internalRelocationChance");
            ValidateProbability(profile.ExternalDepartureChance, ProfilesPath, profile.Id, "externalDepartureChance");
            ValidateProbability(profile.Person.InjuryChance, ProfilesPath, profile.Id, "person.injuryChance");
            ValidateProbability(profile.Person.DeathChance, ProfilesPath, profile.Id, "person.deathChance");
            if (profile.StressGain < 0)
                throw CatalogValidation.Error(ProfilesPath, "a non-negative stress gain", item: profile.Id, field: "stressGain", value: profile.StressGain);

            ValidateNonNegativeRange(profile.WealthLossPercent, ProfilesPath, profile.Id, "wealthLossPercent");
            ValidateNonNegativeRange(profile.Person.HealthDamage, ProfilesPath, profile.Id, "person.healthDamage");
            ValidateNonNegativeRange(profile.HouseRepairCostPercentOfValue, ProfilesPath, profile.Id, "houseRepairCostPercentOfValue");
            ValidateNonNegativeRange(profile.FarmlandLossValuePercent, ProfilesPath, profile.Id, "farmlandLossValuePercent");

            if (profile.Illness is not null)
            {
                ValidateProbability(profile.Illness.ChancePerSelectedMember, ProfilesPath, profile.Id, "illness.chancePerSelectedMember");
                ValidateProbability(profile.Illness.FallbackDeathChance, ProfilesPath, profile.Id, "illness.fallbackDeathChance");
                ValidateNonNegativeRange(profile.Illness.FallbackHealthDamage, ProfilesPath, profile.Id, "illness.fallbackHealthDamage");
                if (profile.Illness.MembersMin < 1 || profile.Illness.MembersMax < profile.Illness.MembersMin)
                    throw CatalogValidation.Error(ProfilesPath, "a valid illness member range", item: profile.Id, field: "illness.members", value: $"{profile.Illness.MembersMin}-{profile.Illness.MembersMax}");
            }
        }

        foreach (var route in routes.Values)
        {
            if (!route.Type.Equals("internal_household", StringComparison.OrdinalIgnoreCase)
                && !route.Type.Equals("external_household", StringComparison.OrdinalIgnoreCase)
                && !route.Type.Equals("external_branch", StringComparison.OrdinalIgnoreCase))
            {
                throw CatalogValidation.Error(RoutesPath, "a supported migration route type", item: route.Id, field: "type", value: route.Type);
            }

            if (route.Type.Equals("internal_household", StringComparison.OrdinalIgnoreCase)
                && route.TargetRegionIds is not { Count: > 0 })
            {
                throw CatalogValidation.Error(RoutesPath, "at least one target region for internal relocation", item: route.Id, field: "targetRegionIds");
            }

            if (route.Type.StartsWith("external", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(route.DestinationLabel))
            {
                throw CatalogValidation.Error(RoutesPath, "a destination label for external migration", item: route.Id, field: "destinationLabel", value: route.DestinationLabel);
            }

            ValidateProbability(route.ReplacementHouseChance, RoutesPath, route.Id, "replacementHouseChance");
            ValidateProbability(route.ReplacementFarmlandChance, RoutesPath, route.Id, "replacementFarmlandChance");
            ValidateProbability(route.ReplacementFarmlandChanceIfPreviouslyOwned, RoutesPath, route.Id, "replacementFarmlandChanceIfPreviouslyOwned");
            if (route.CashRetention is { Length: > 0 })
            {
                ValidateNonNegativeRange(route.CashRetention, RoutesPath, route.Id, "cashRetention");
                if (route.CashRetention.Any(value => value > 1))
                    throw CatalogValidation.Error(RoutesPath, "cash retention fractions in 0..1", item: route.Id, field: "cashRetention", value: string.Join(",", route.CashRetention));
            }

            foreach (var regionId in route.TargetRegionIds ?? [])
            {
                if (!knownRegions.Contains(regionId))
                    throw CatalogValidation.Error(RoutesPath, "a Town RegionId", item: route.Id, field: "targetRegionIds", value: regionId);
            }
            foreach (var placeId in route.ExcludePlaceIds ?? [])
            {
                if (!knownPlaces.Contains(placeId))
                    throw CatalogValidation.Error(RoutesPath, "a Town PlaceId", item: route.Id, field: "excludePlaceIds", value: placeId);
            }
        }

        foreach (var modifier in modifiers)
        {
            if (!events.Any(item => item.Id.Equals(modifier.EventId, StringComparison.OrdinalIgnoreCase)))
                throw CatalogValidation.Error(ModifiersPath, "a known EventId", item: modifier.Id, field: "EventId", value: modifier.EventId);
            if (!scopes.ContainsKey(modifier.ScopeId))
                throw CatalogValidation.Error(ModifiersPath, "a known ScopeId", item: modifier.Id, field: "ScopeId", value: modifier.ScopeId);
            _ = nationalities.GetDisplayName(modifier.NationalityId);
            if (modifier.WeightMultiplier < 0)
                throw CatalogValidation.Error(ModifiersPath, "a non-negative multiplier", item: modifier.Id, field: "WeightMultiplier", value: modifier.WeightMultiplier);
        }

        var orderedPeriods = periods.OrderBy(item => item.StartYear).ToList();
        if (orderedPeriods.Count == 0 || orderedPeriods[0].StartYear != 1700)
            throw CatalogValidation.Error(PeriodsPath, "period coverage beginning in 1700");
        for (var i = 1; i < orderedPeriods.Count; i++)
        {
            if (orderedPeriods[i].StartYear != orderedPeriods[i - 1].EndYear + 1)
                throw CatalogValidation.Error(PeriodsPath, "continuous non-overlapping period coverage", item: orderedPeriods[i].Id);
        }
        if (orderedPeriods[^1].EndYear < 2026)
            throw CatalogValidation.Error(PeriodsPath, "period coverage through at least 2026", item: orderedPeriods[^1].Id, field: "EndYear", value: orderedPeriods[^1].EndYear);
    }

    private static void ValidateProbability(
        double value,
        string path,
        string item,
        string field)
    {
        if (value is < 0 or > 1)
            throw CatalogValidation.Error(path, "a probability in 0..1", item: item, field: field, value: value);
    }

    private static void ValidateNonNegativeRange(
        double[]? range,
        string path,
        string item,
        string field)
    {
        if (range is null || range.Length == 0)
            return;
        if (range.Length > 2 || range.Any(value => value < 0))
            throw CatalogValidation.Error(path, "one or two non-negative range values", item: item, field: field, value: string.Join(",", range));
        if (range.Length == 2 && range[1] < range[0])
            throw CatalogValidation.Error(path, "an ascending range", item: item, field: field, value: string.Join(",", range));
    }

    private static List<Dictionary<string, string>> ParseCsv(string text, string path)
    {
        var lines = text.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        if (lines.Count < 2)
            throw CatalogValidation.Error(path, "a CSV header and at least one row");

        var headers = ParseCsvLine(lines[0].TrimStart('\uFEFF'));
        var rows = new List<Dictionary<string, string>>();
        for (var lineIndex = 1; lineIndex < lines.Count; lineIndex++)
        {
            var values = ParseCsvLine(lines[lineIndex]);
            if (values.Count != headers.Count)
                throw CatalogValidation.FieldCount(path, lineIndex + 1, values.Count, headers.Count);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
                row[headers[i]] = values[i];
            rows.Add(row);
        }
        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (ch == ',' && !quoted)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        fields.Add(current.ToString().Trim());
        return fields;
    }

    private static string Require(IReadOnlyDictionary<string, string> row, string key, string path)
    {
        if (!row.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw CatalogValidation.Error(path, "a non-empty value", field: key, value: value);
        return value.Trim();
    }

    private static string? Optional(IReadOnlyDictionary<string, string> row, string key) =>
        row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static int ParseInt(IReadOnlyDictionary<string, string> row, string key, string path) =>
        int.TryParse(Require(row, key, path), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw CatalogValidation.Error(path, "an integer", field: key, value: row[key]);

    private static double ParseDouble(IReadOnlyDictionary<string, string> row, string key, string path) =>
        double.TryParse(Require(row, key, path), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw CatalogValidation.Error(path, "a number", field: key, value: row[key]);

    private static bool ParseBool01(IReadOnlyDictionary<string, string> row, string key, string path) =>
        Require(row, key, path) switch
        {
            "1" => true,
            "0" => false,
            var raw => throw CatalogValidation.Error(path, "0 or 1", field: key, value: raw)
        };
}

internal sealed record HistoricalScheduledEvent(
    string Id,
    string DisplayName,
    int StartYear,
    int EndYear,
    string TriggerMode,
    string ScopeId,
    string TargetFilterId,
    string EffectProfileId,
    string? MigrationRouteId,
    string? ExclusiveGroup,
    int Priority,
    string PlayerImpactPolicy,
    bool ChronicleAtStart,
    bool HouseholdNews,
    string? SourceGroupId,
    string GlobalNewsDescription)
{
    public bool IsActive(int year) => year >= StartYear && year <= EndYear;
}

internal sealed class HistoricalScopeDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int? Year { get; set; }
    public List<string>? PolityIds { get; set; }
    public List<string>? FromPolityIds { get; set; }
    public List<string>? ToPolityIds { get; set; }
    public bool? FromPolish { get; set; }
    public bool? ToPolish { get; set; }
    public Dictionary<string, double>? RegionMultipliers { get; set; }
    public Dictionary<string, double>? PlaceMultipliers { get; set; }
    public List<string>? ScopeIds { get; set; }
}

internal sealed class HistoricalTargetFilterDefinition
{
    public string Id { get; set; } = string.Empty;
    public string HouseholdEligibility { get; set; } = string.Empty;
    public List<string>? NationalityIds { get; set; }
    public string PersonEffects { get; set; } = "all_members";
    public string HouseholdEffects { get; set; } = "whole_household";
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
}

internal sealed class HistoricalEffectProfile
{
    public string Id { get; set; } = string.Empty;
    public double HouseholdExposureChance { get; set; }
    public double[]? WealthLossPercent { get; set; }
    public double StressGain { get; set; }
    public double JobLossChance { get; set; }
    public double ImprisonmentChance { get; set; }
    public double PropertyConfiscationChance { get; set; }
    public double HouseRepairCostChance { get; set; }
    public double[]? HouseRepairCostPercentOfValue { get; set; }
    public double[]? FarmlandLossValuePercent { get; set; }
    public double InternalRelocationChance { get; set; }
    public double ExternalDepartureChance { get; set; }
    public HistoricalPersonEffect Person { get; set; } = new();
    public HistoricalIllnessEffect? Illness { get; set; }
    public Dictionary<string, double>? PersonRiskMultipliers { get; set; }
    public Dictionary<string, double>? HouseholdExposureMultipliers { get; set; }
}

internal sealed class HistoricalPersonEffect
{
    public double InjuryChance { get; set; }
    public double[]? HealthDamage { get; set; }
    public double DeathChance { get; set; }
}

internal sealed class HistoricalIllnessEffect
{
    public string ConditionId { get; set; } = string.Empty;
    public int MembersMin { get; set; } = 1;
    public int MembersMax { get; set; } = 1;
    public double ChancePerSelectedMember { get; set; } = 1;
    public double[]? FallbackHealthDamage { get; set; }
    public double FallbackDeathChance { get; set; }
}

internal sealed class HistoricalMigrationRoute
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public List<string>? TargetRegionIds { get; set; }
    public Dictionary<string, double>? RegionWeights { get; set; }
    public List<string>? ExcludePlaceIds { get; set; }
    public string? DestinationWeighting { get; set; }
    public string? DestinationLabel { get; set; }
    public double[]? CashRetention { get; set; }
    public JsonElement LoseOriginHouse { get; set; }
    public JsonElement LoseOriginFarmland { get; set; }
    public double ReplacementHouseChance { get; set; }
    public double ReplacementFarmlandChance { get; set; }
    public double ReplacementFarmlandChanceIfPreviouslyOwned { get; set; }
    public string? PlayerControlledPolicy { get; set; }
}

internal sealed record HistoricalCandidateModifier(
    string Id,
    string EventId,
    int StartYear,
    int EndYear,
    string ScopeId,
    string NationalityId,
    double WeightMultiplier,
    int Priority)
{
    public bool IsActive(int year) => year >= StartYear && year <= EndYear;
}

internal sealed record HistoricalPeriodDefinition(
    string Id,
    int StartYear,
    int EndYear,
    string DisplayName);
