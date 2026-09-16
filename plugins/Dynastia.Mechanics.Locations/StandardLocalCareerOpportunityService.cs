using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed class StandardLocalCareerOpportunityService :
    ILocalCareerOpportunityService
{
    private const string OpportunityTagsPath =
        "Towns/opportunity_tags.csv";

    private const string RegionOpportunitiesPath =
        "Towns/region_opportunities.csv";

    private const string TownOpportunitiesPath =
        "Towns/town_opportunities.csv";

    private const double RegionalSpecialistMultiplier =
        1.45;

    private const double TownSpecialistMultiplier =
        2.25;

    private readonly IGameState _gameState;
    private readonly ILocationService _locations;

    private readonly IReadOnlyDictionary<
        string,
        OpportunityTagInfo>
        _opportunityTags;

    private readonly IReadOnlyDictionary<
        string,
        RegionOpportunityInfo>
        _regions;

    private readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<TimedOpportunity>>
        _townOpportunities;

    public StandardLocalCareerOpportunityService(
        IGameState gameState,
        ILocationService locations,
        IGameDataService data)
    {
        _gameState = gameState;
        _locations = locations;

        _opportunityTags =
            ParseOpportunityTags(
                data.ReadText(
                    OpportunityTagsPath));

        _regions =
            ParseRegions(
                data.ReadText(
                    RegionOpportunitiesPath));

        _townOpportunities =
            ParseTownOpportunities(
                data.ReadText(
                    TownOpportunitiesPath));

        ValidateReferences();
    }

    public CareerLocationEvaluation Evaluate(
        IPerson person,
        CareerLocationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(person);

        return EvaluateProfile(
            GetOpportunitySnapshot(person),
            requirement);
    }

    public CareerLocationEvaluation Evaluate(
        TownInfo town,
        CareerLocationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(town);

        return EvaluateProfile(
            GetOpportunitySnapshot(town),
            requirement);
    }

    private static CareerLocationEvaluation EvaluateProfile(
        LocationOpportunitySnapshot profile,
        CareerLocationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        var townClass =
            profile.Town.SettlementClass;

        if ((int)townClass
            < (int)requirement.MinimumSettlementClass)
        {
            return Unavailable();
        }

        return requirement.LocationType switch
        {
            CareerLocationType.Generic =>
                EvaluateGeneric(
                    townClass,
                    requirement.MinimumSettlementClass),

            CareerLocationType.Urban =>
                EvaluateUrban(
                    townClass,
                    requirement.MinimumSettlementClass),

            CareerLocationType.Specialist =>
                EvaluateSpecialist(
                    profile,
                    requirement.RequiredOpportunityTags),

            _ => Unavailable()
        };
    }

    public LocationOpportunitySnapshot
        GetOpportunitySnapshot(
            IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return GetOpportunitySnapshot(
            _locations.GetLocation(person).HomeTown);
    }

    public LocationOpportunitySnapshot
        GetOpportunitySnapshot(
            TownInfo town)
    {
        ArgumentNullException.ThrowIfNull(town);

        var effectiveYear =
            Math.Min(
                _gameState.Year,
                GameCalendarConfiguration.TechnologyFreezeYear);

        var region =
            ResolveRegion(
                town.RegionId,
                effectiveYear);

        var townTags =
            !string.IsNullOrWhiteSpace(town.Id)
            && _townOpportunities.TryGetValue(
                town.Id,
                out var configuredTownTags)
                ? GetActiveTags(
                    configuredTownTags,
                    effectiveYear)
                : Array.Empty<string>();

        var description =
            BuildDescription(
                region.OpportunityTags,
                townTags);

        return new LocationOpportunitySnapshot(
            town,
            region.Name,
            region.OpportunityTags,
            townTags,
            description);
    }

    private static CareerLocationEvaluation
        EvaluateGeneric(
            SettlementClass actual,
            SettlementClass minimum)
    {
        var classDifference =
            (int)actual
            - (int)minimum;

        var multiplier =
            1.0
            + Math.Min(
                0.15,
                Math.Max(
                    0,
                    classDifference)
                * 0.05);

        return new CareerLocationEvaluation(
            true,
            multiplier,
            CareerOpportunityStrength.Generic);
    }

    private static CareerLocationEvaluation
        EvaluateUrban(
            SettlementClass actual,
            SettlementClass minimum)
    {
        var multiplier =
            actual switch
            {
                SettlementClass.SmallTown => 0.70,
                SettlementClass.Town => 0.85,
                SettlementClass.City => 1.00,
                SettlementClass.MajorCity => 1.25,
                _ => 1.00
            };

        if (minimum == SettlementClass.MajorCity)
        {
            multiplier = 1.0;
        }

        return new CareerLocationEvaluation(
            true,
            multiplier,
            CareerOpportunityStrength.Generic);
    }

    private static CareerLocationEvaluation
        EvaluateSpecialist(
            LocationOpportunitySnapshot profile,
            IReadOnlyCollection<string> requiredTags)
    {
        if (requiredTags.Count == 0)
            return Unavailable();

        var required =
            requiredTags.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        if (profile.TownOpportunityTags
            .Any(required.Contains))
        {
            return new CareerLocationEvaluation(
                true,
                TownSpecialistMultiplier,
                CareerOpportunityStrength.Town);
        }

        if (profile.RegionOpportunityTags
            .Any(required.Contains))
        {
            return new CareerLocationEvaluation(
                true,
                RegionalSpecialistMultiplier,
                CareerOpportunityStrength.Regional);
        }

        return Unavailable();
    }

    private RegionOpportunitySnapshot ResolveRegion(
        string regionId,
        int effectiveYear)
    {
        if (!string.IsNullOrWhiteSpace(regionId)
            && _regions.TryGetValue(
                regionId,
                out var region))
        {
            return new RegionOpportunitySnapshot(
                region.Name,
                GetActiveTags(
                    region.Opportunities,
                    effectiveYear));
        }

        return new RegionOpportunitySnapshot(
            "Unknown region",
            Array.Empty<string>());
    }

    private IReadOnlyList<string> GetActiveTags(
        IReadOnlyList<TimedOpportunity> opportunities,
        int effectiveYear)
    {
        return opportunities
            .Where(opportunity =>
                opportunity.IsActive(effectiveYear)
                && _opportunityTags[opportunity.Tag]
                    .IsActive(effectiveYear))
            .Select(opportunity =>
                opportunity.Tag)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string BuildDescription(
        IReadOnlyList<string> regionTags,
        IReadOnlyList<string> townTags)
    {
        if (townTags.Count > 0)
        {
            return
                "Strong local opportunities in "
                + JoinNatural(
                    townTags
                        .Take(4)
                        .Select(
                            FormatOpportunityTag)
                        .ToArray())
                + ".";
        }

        if (regionTags.Count > 0)
        {
            return
                "Regional opportunities include "
                + JoinNatural(
                    regionTags
                        .Take(4)
                        .Select(
                            FormatOpportunityTag)
                        .ToArray())
                + ".";
        }

        return
            "Employment is dominated by general local work and services.";
    }

    private string FormatOpportunityTag(
        string tag)
    {
        return _opportunityTags.TryGetValue(
            tag,
            out var info)
                ? info.DisplayName
                : tag.Replace('_', ' ');
    }

    private static string JoinNatural(
        IReadOnlyList<string> values)
    {
        return values.Count switch
        {
            0 => string.Empty,
            1 => values[0],
            2 => $"{values[0]} and {values[1]}",
            _ =>
                string.Join(
                    ", ",
                    values.Take(values.Count - 1))
                + $" and {values[^1]}"
        };
    }

    private IReadOnlyDictionary<
        string,
        OpportunityTagInfo>
        ParseOpportunityTags(
            string text)
    {
        var lines = SplitLines(text);

        ValidateHeader(
            lines,
            OpportunityTagsPath,
            "Tag,DisplayName,DefaultStartYear,EndYear");

        var result =
            new Dictionary<string, OpportunityTagInfo>(
                StringComparer.OrdinalIgnoreCase);

        for (var index = 1;
            index < lines.Length;
            index++)
        {
            var fields = lines[index].Split(',');

            if (fields.Length != 4)
                InvalidRow(OpportunityTagsPath, index, 4);

            var tag = fields[0].Trim();
            var displayName = fields[1].Trim();
            var startYear = ParseYear(fields[2], OpportunityTagsPath, index);
            var endYear = ParseOptionalYear(fields[3], OpportunityTagsPath, index);

            ValidatePeriod(
                OpportunityTagsPath,
                index,
                startYear,
                endYear);

            if (tag.Length == 0
                || displayName.Length == 0
                || !result.TryAdd(
                    tag,
                    new OpportunityTagInfo(
                        tag,
                        displayName,
                        startYear,
                        endYear)))
            {
                throw new InvalidDataException(
                    $"Invalid or duplicate opportunity tag in " +
                    $"{OpportunityTagsPath} row {index + 1}.");
            }
        }

        return result;
    }

    private IReadOnlyDictionary<
        string,
        RegionOpportunityInfo>
        ParseRegions(
            string text)
    {
        var lines = SplitLines(text);

        ValidateHeader(
            lines,
            RegionOpportunitiesPath,
            "RegionId,RegionName,OpportunityTag,StartYear,EndYear");

        var working =
            new Dictionary<
                string,
                (string Name, List<TimedOpportunity> Opportunities)>(
                StringComparer.OrdinalIgnoreCase);

        for (var index = 1;
            index < lines.Length;
            index++)
        {
            var fields = lines[index].Split(',');

            if (fields.Length != 5)
                InvalidRow(RegionOpportunitiesPath, index, 5);

            var id = fields[0].Trim();
            var name = fields[1].Trim();
            var opportunity = ParseTimedOpportunity(
                fields[2],
                fields[3],
                fields[4],
                RegionOpportunitiesPath,
                index);

            if (id.Length == 0
                || name.Length == 0)
            {
                throw new InvalidDataException(
                    $"Invalid {RegionOpportunitiesPath} row " +
                    $"{index + 1}: region ID and name are required.");
            }

            if (!working.TryGetValue(id, out var entry))
            {
                entry = (name, []);
                working[id] = entry;
            }
            else if (!entry.Name.Equals(
                name,
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Region '{id}' has inconsistent names in " +
                    $"{RegionOpportunitiesPath}.");
            }

            entry.Opportunities.Add(opportunity);
        }

        return working.ToDictionary(
            pair => pair.Key,
            pair => new RegionOpportunityInfo(
                pair.Key,
                pair.Value.Name,
                pair.Value.Opportunities.ToArray()),
            StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyDictionary<
        string,
        IReadOnlyList<TimedOpportunity>>
        ParseTownOpportunities(
            string text)
    {
        var lines = SplitLines(text);

        ValidateHeader(
            lines,
            TownOpportunitiesPath,
            "TownId,OpportunityTag,StartYear,EndYear");

        var working =
            new Dictionary<string, List<TimedOpportunity>>(
                StringComparer.OrdinalIgnoreCase);

        for (var index = 1;
            index < lines.Length;
            index++)
        {
            var fields = lines[index].Split(',');

            if (fields.Length != 4)
                InvalidRow(TownOpportunitiesPath, index, 4);

            var townId = fields[0].Trim();

            if (townId.Length == 0)
            {
                throw new InvalidDataException(
                    $"Invalid {TownOpportunitiesPath} row " +
                    $"{index + 1}: town ID is required.");
            }

            var opportunity = ParseTimedOpportunity(
                fields[1],
                fields[2],
                fields[3],
                TownOpportunitiesPath,
                index);

            if (!working.TryGetValue(townId, out var entries))
            {
                entries = [];
                working[townId] = entries;
            }

            entries.Add(opportunity);
        }

        return working.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<TimedOpportunity>)pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private TimedOpportunity ParseTimedOpportunity(
        string tagField,
        string startField,
        string endField,
        string path,
        int rowIndex)
    {
        var tag = tagField.Trim();

        if (!_opportunityTags.ContainsKey(tag))
        {
            throw new InvalidDataException(
                $"Unknown opportunity tag '{tag}' in " +
                $"{path} row {rowIndex + 1}.");
        }

        var startYear =
            ParseYear(startField, path, rowIndex);

        var endYear =
            ParseOptionalYear(endField, path, rowIndex);

        ValidatePeriod(
            path,
            rowIndex,
            startYear,
            endYear);

        return new TimedOpportunity(
            tag,
            startYear,
            endYear);
    }

    private void ValidateReferences()
    {
        var towns =
            _locations.GetTowns();

        var knownTownIds =
            towns.Select(town => town.Id)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var knownRegionIds =
            towns.Select(town => town.RegionId)
                .Where(id =>
                    !string.IsNullOrWhiteSpace(id))
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        foreach (var townId in _townOpportunities.Keys)
        {
            if (!knownTownIds.Contains(townId))
            {
                throw new InvalidDataException(
                    $"{TownOpportunitiesPath} references unknown " +
                    $"town '{townId}'.");
            }
        }

        foreach (var regionId in _regions.Keys)
        {
            if (!knownRegionIds.Contains(regionId))
            {
                throw new InvalidDataException(
                    $"{RegionOpportunitiesPath} references unknown " +
                    $"region '{regionId}'.");
            }
        }
    }

    private static string[] SplitLines(
        string text)
    {
        return text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);
    }

    private static void ValidateHeader(
        IReadOnlyList<string> lines,
        string path,
        string expectedHeader)
    {
        if (lines.Count < 2)
        {
            throw new InvalidDataException(
                $"{path} is empty.");
        }

        if (!lines[0].Equals(
                expectedHeader,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{path} has an unexpected header.");
        }
    }

    private static int ParseYear(
        string value,
        string path,
        int rowIndex)
    {
        if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var year))
        {
            throw new InvalidDataException(
                $"Invalid year in {path} row {rowIndex + 1}.");
        }

        return year;
    }

    private static int? ParseOptionalYear(
        string value,
        string path,
        int rowIndex)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : ParseYear(value, path, rowIndex);
    }

    private static void ValidatePeriod(
        string path,
        int rowIndex,
        int startYear,
        int? endYear)
    {
        if (startYear < GameCalendarConfiguration.GameStartYear)
        {
            throw new InvalidDataException(
                $"{path} row {rowIndex + 1} starts before " +
                $"{GameCalendarConfiguration.GameStartYear}.");
        }

        if (endYear is int end
            && end < startYear)
        {
            throw new InvalidDataException(
                $"{path} row {rowIndex + 1} ends before it starts.");
        }
    }

    private static void InvalidRow(
        string path,
        int rowIndex,
        int expectedFields)
    {
        throw new InvalidDataException(
            $"Invalid {path} row {rowIndex + 1}: expected " +
            $"{expectedFields} fields.");
    }

    private static CareerLocationEvaluation Unavailable()
    {
        return new CareerLocationEvaluation(
            false,
            0,
            CareerOpportunityStrength.None);
    }

    private sealed record OpportunityTagInfo(
        string Tag,
        string DisplayName,
        int StartYear,
        int? EndYear)
    {
        public bool IsActive(int year) =>
            year >= StartYear
            && (EndYear is null
                || year <= EndYear.Value);
    }

    private sealed record TimedOpportunity(
        string Tag,
        int StartYear,
        int? EndYear)
    {
        public bool IsActive(int year) =>
            year >= StartYear
            && (EndYear is null
                || year <= EndYear.Value);
    }

    private sealed record RegionOpportunityInfo(
        string Id,
        string Name,
        IReadOnlyList<TimedOpportunity> Opportunities);

    private sealed record RegionOpportunitySnapshot(
        string Name,
        IReadOnlyList<string> OpportunityTags);
}
