using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed class StandardLocalCareerOpportunityService :
    ILocalCareerOpportunityService
{
    private const string RegionOpportunitiesPath =
        "Towns/region_opportunities.csv";

    private const string TownOpportunitiesPath =
        "Towns/town_opportunities.csv";

    private const double RegionalSpecialistMultiplier =
        1.45;

    private const double TownSpecialistMultiplier =
        2.25;

    private readonly ILocationService _locations;

    private readonly IReadOnlyDictionary<
        string,
        RegionOpportunityInfo>
        _regions;

    private readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<string>>
        _townOpportunities;

    public StandardLocalCareerOpportunityService(
        ILocationService locations,
        IGameDataService data)
    {
        _locations = locations;

        _regions =
            ParseRegions(
                data.ReadText(
                    RegionOpportunitiesPath));

        _townOpportunities =
            ParseTownOpportunities(
                data.ReadText(
                    TownOpportunitiesPath));
    }

    public CareerLocationEvaluation Evaluate(
        IPerson person,
        CareerLocationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        ArgumentNullException.ThrowIfNull(
            requirement);

        var profile =
            GetOpportunitySnapshot(
                person);

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
        ArgumentNullException.ThrowIfNull(
            person);

        return GetOpportunitySnapshot(
            _locations.GetLocation(person).HomeTown);
    }

    public LocationOpportunitySnapshot
        GetOpportunitySnapshot(
            TownInfo town)
    {
        ArgumentNullException.ThrowIfNull(
            town);

        var region =
            ResolveRegion(
                town.RegionId);

        var townTags =
            !string.IsNullOrWhiteSpace(
                    town.Id)
                && _townOpportunities.TryGetValue(
                    town.Id,
                    out var configuredTownTags)
                    ? configuredTownTags
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

        // Generic work exists almost everywhere. Larger settlements
        // modestly broaden the pool without making local trades
        // irrelevant in smaller places.
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

        // A career whose minimum is already a major city should not
        // receive a further bonus simply for meeting that threshold.
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

    private RegionOpportunityInfo ResolveRegion(
        string regionId)
    {
        if (!string.IsNullOrWhiteSpace(
                regionId)
            && _regions.TryGetValue(
                regionId,
                out var region))
        {
            return region;
        }

        return new RegionOpportunityInfo(
            string.IsNullOrWhiteSpace(
                regionId)
                ? "unknown"
                : regionId,
            "Unknown region",
            Array.Empty<string>());
    }

    private static string BuildDescription(
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

    private static string FormatOpportunityTag(
        string tag)
    {
        return tag switch
        {
            "coal" => "coal mining",
            "steel" => "steel",
            "heavy_industry" => "heavy industry",
            "manufacturing" => "manufacturing",
            "agriculture" => "agriculture",
            "forestry" => "forestry",
            "port" => "port work",
            "shipping" => "shipping",
            "shipbuilding" => "shipbuilding",
            "textiles" => "textiles",
            "automotive" => "automotive manufacturing",
            "chemicals" => "chemicals",
            "aviation" => "aviation",
            "technology" => "technology",
            "oil" => "oil and refining",
            "tourism" => "tourism",
            "finance" => "finance",
            "media" => "media",
            "food" => "food production",
            "electric_power" => "electric power",
            _ => tag.Replace('_', ' ')
        };
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
                    values.Take(
                        values.Count - 1))
                + $" and {values[^1]}"
        };
    }

    private static IReadOnlyDictionary<
        string,
        RegionOpportunityInfo>
        ParseRegions(
            string text)
    {
        var lines =
            text.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
        {
            throw new InvalidDataException(
                $"{RegionOpportunitiesPath} is empty.");
        }

        var result =
            new Dictionary<
                string,
                RegionOpportunityInfo>(
                StringComparer.OrdinalIgnoreCase);

        for (var index = 1;
            index < lines.Length;
            index++)
        {
            var fields =
                lines[index].Split(',');

            if (fields.Length != 3)
            {
                throw new InvalidDataException(
                    $"Invalid {RegionOpportunitiesPath} row "
                    + $"{index + 1}: expected 3 fields.");
            }

            var id = fields[0].Trim();
            var name = fields[1].Trim();
            var tags = ParseTags(fields[2]);

            if (id.Length == 0
                || name.Length == 0)
            {
                throw new InvalidDataException(
                    $"Invalid {RegionOpportunitiesPath} row "
                    + $"{index + 1}: region ID and name are required.");
            }

            if (!result.TryAdd(
                    id,
                    new RegionOpportunityInfo(
                        id,
                        name,
                        tags)))
            {
                throw new InvalidDataException(
                    $"Duplicate region ID '{id}'.");
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<
        string,
        IReadOnlyList<string>>
        ParseTownOpportunities(
            string text)
    {
        var lines =
            text.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries);

        var result =
            new Dictionary<
                string,
                IReadOnlyList<string>>(
                StringComparer.OrdinalIgnoreCase);

        for (var index = 1;
            index < lines.Length;
            index++)
        {
            var fields =
                lines[index].Split(',');

            if (fields.Length != 2)
            {
                throw new InvalidDataException(
                    $"Invalid {TownOpportunitiesPath} row "
                    + $"{index + 1}: expected 2 fields.");
            }

            var townId = fields[0].Trim();

            if (townId.Length == 0)
            {
                throw new InvalidDataException(
                    $"Invalid {TownOpportunitiesPath} row "
                    + $"{index + 1}: town ID is required.");
            }

            if (!result.TryAdd(
                    townId,
                    ParseTags(fields[1])))
            {
                throw new InvalidDataException(
                    $"Duplicate town opportunity entry '{townId}'.");
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ParseTags(
        string value)
    {
        return value
            .Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static CareerLocationEvaluation Unavailable()
    {
        return new CareerLocationEvaluation(
            false,
            0,
            CareerOpportunityStrength.None);
    }

    private sealed record RegionOpportunityInfo(
        string Id,
        string Name,
        IReadOnlyList<string> OpportunityTags);
}
