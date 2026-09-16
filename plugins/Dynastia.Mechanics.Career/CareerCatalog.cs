using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal sealed class CareerCatalog
{
    private const string DataPath =
        "Career/careers.csv";

    private const string OpportunityTagsPath =
        "Towns/opportunity_tags.csv";


    private static readonly IReadOnlySet<string>
        ExpectedCareerIds =
            new HashSet<string>(
                new[]
                {
                    "agriculture_and_farm_estates",
                    "horse_and_carriage_trade",
                    "blacksmithing",
                    "coal_mining",
                    "oil_and_refining",
                    "steel_and_foundry",
                    "textile_mills",
                    "timber_and_sawmills",
                    "construction",
                    "railways",
                    "shipping_and_ports",
                    "mass_factory_manufacturing",
                    "traditional_printworks",
                    "newspapers_and_publishing",
                    "retail_trade",
                    "banking",
                    "insurance",
                    "hotels_and_restaurants",
                    "food_processing",
                    "baking_and_confectionery",
                    "meat_trade",
                    "tailoring_and_fashion",
                    "leather_and_shoemaking",
                    "furniture_and_carpentry",
                    "glass_and_ceramics",
                    "jewellery_and_watchmaking",
                    "photography",
                    "domestic_service",
                    "laundry_and_dry_cleaning",
                    "funeral_services",
                    "real_estate",
                    "accounting",
                    "legal_services",
                    "healthcare_services",
                    "pharmacy",
                    "education",
                    "electric_power",
                    "cinema_and_film",
                    "advertising",
                    "automotive_industry",
                    "chemical_industry",
                    "beauty_and_cosmetics",
                    "radio_broadcasting",
                    "aviation",
                    "road_haulage",
                    "consumer_goods_and_appliances",
                    "tourism_and_travel",
                    "plastics_industry",
                    "electronics_manufacturing",
                    "pharmaceuticals",
                    "television",
                    "telecommunications",
                    "engineering_services",
                    "logistics_and_warehousing",
                    "computing_and_it_services",
                    "investment_and_financial_services",
                    "private_security",
                    "biotechnology",
                    "software_industry",
                    "video_game_industry",
                    "business_process_outsourcing",
                    "e_commerce",
                    "digital_media",
                    "cybersecurity",
                    "renewable_energy",
                },
                StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyList<CareerDefinition>
        _careers;

    private readonly IReadOnlyDictionary<
        string,
        CareerDefinition>
        _byId;

    private CareerCatalog(
        IReadOnlyList<CareerDefinition> careers)
    {
        _careers =
            careers;

        _byId =
            careers.ToDictionary(
                career =>
                    career.Id,
                StringComparer.OrdinalIgnoreCase);
    }

    internal IReadOnlyCollection<string> CareerIds =>
        _byId.Keys.ToArray();

    internal IReadOnlyList<CareerDefinition> All =>
        _careers;

    public static CareerCatalog Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(
            data);

        var careers =
            Parse(
                data.ReadText(
                    DataPath));

        var knownOpportunityTags =
            ParseOpportunityTags(
                data.ReadText(
                    OpportunityTagsPath));

        Validate(
            careers,
            knownOpportunityTags);

        return new CareerCatalog(
            careers);
    }

    public CareerDefinition? Find(
        string? id)
    {
        if (string.IsNullOrWhiteSpace(
            id))
        {
            return null;
        }

        return _byId.TryGetValue(
            id,
            out var career)
                ? career
                : null;
    }

    public CareerDefinition SelectForEntry(
        Sex sex,
        int gameYear,
        IGameRandom random,
        Func<CareerDefinition, double>?
            availabilityMultiplier = null)
    {
        var available =
            _careers
                .Where(
                    career =>
                        career.IsOpenForEntry(
                            gameYear))
                .Select(
                    career =>
                    {
                        var locationMultiplier =
                            availabilityMultiplier?.Invoke(
                                career)
                            ?? 1.0;

                        return new WeightedCareer(
                            career,
                            career.GetEntryWeight(
                                sex,
                                gameYear)
                            * Math.Max(
                                0,
                                locationMultiplier));
                    })
                .Where(
                    entry =>
                        entry.Weight > 0)
                .ToList();

        if (available.Count == 0)
        {
            throw new InvalidOperationException(
                $"No career is available for entry " +
                $"in year {gameYear}.");
        }

        var total =
            available.Sum(
                entry =>
                    entry.Weight);

        var roll =
            random.NextDouble()
            * total;

        foreach (var entry in
            available)
        {
            if (roll < entry.Weight)
                return entry.Career;

            roll -=
                entry.Weight;
        }

        return available[^1].Career;
    }

    private static IReadOnlyList<CareerDefinition>
        Parse(
            string text)
    {
        var lines =
            text.Split(
                ['\r', '\n'],
                StringSplitOptions
                    .RemoveEmptyEntries);

        if (lines.Length < 2)
        {
            throw new InvalidDataException(
                $"{DataPath} is empty.");
        }

        var header =
            lines[0].Split(',');

        var expectedHeader =
            new[]
            {
                "Number",
                "Id",
                "Name",
                "Emoji",
                "StartYear",
                "EndYear",
                "MaleEarly",
                "FemaleEarly",
                "MaleLate",
                "FemaleLate",
                "BaseSalary",
                "Level1Title",
                "Level2Title",
                "Level3Title",
                "Level4Title",
                "Level5Title",
                "LocationType",
                "MinimumSettlementClass",
                "RequiredOpportunityTags"
            };

        var legacyHeader =
            new[]
            {
                "Number",
                "Id",
                "Name",
                "StartYear",
                "EndYear",
                "MaleEarly",
                "FemaleEarly",
                "MaleLate",
                "FemaleLate",
                "BaseSalary",
                "Level1Title",
                "Level2Title",
                "Level3Title",
                "Level4Title",
                "Level5Title",
                "LocationType",
                "MinimumSettlementClass",
                "RequiredOpportunityTags"
            };

        var hasEmojiColumn =
            header.SequenceEqual(
                expectedHeader,
                StringComparer.Ordinal);

        var isLegacyHeader =
            header.SequenceEqual(
                legacyHeader,
                StringComparer.Ordinal);

        if (!hasEmojiColumn
            && !isLegacyHeader)
        {
            throw new InvalidDataException(
                $"{DataPath} has an unexpected header: " +
                string.Join(",", header));
        }

        var result =
            new List<CareerDefinition>();

        for (var index = 1;
            index < lines.Length;
            index++)
        {
            var fields =
                lines[index].Split(',');

            var expectedFieldCount =
                hasEmojiColumn
                    ? expectedHeader.Length
                    : legacyHeader.Length;

            if (fields.Length
                != expectedFieldCount)
            {
                throw new InvalidDataException(
                    $"{DataPath} row {index + 1} " +
                    $"has {fields.Length} fields; " +
                    $"expected {expectedFieldCount}.");
            }

            var offset =
                hasEmojiColumn
                    ? 1
                    : 0;

            result.Add(
                new CareerDefinition(
                    Id:
                        fields[1],
                    Name:
                        fields[2],
                    Emoji:
                        hasEmojiColumn
                            ? fields[3]
                            : "💼",
                    StartYear:
                        ParseInt(
                            fields[3 + offset],
                            index),
                    EndYear:
                        string.IsNullOrWhiteSpace(
                            fields[4 + offset])
                            ? null
                            : ParseInt(
                                fields[4 + offset],
                                index),
                    MaleEarly:
                        ParseInt(
                            fields[5 + offset],
                            index),
                    FemaleEarly:
                        ParseInt(
                            fields[6 + offset],
                            index),
                    MaleLate:
                        ParseInt(
                            fields[7 + offset],
                            index),
                    FemaleLate:
                        ParseInt(
                            fields[8 + offset],
                            index),
                    BaseSalary:
                        ParseDecimal(
                            fields[9 + offset],
                            index),
                    Level1Title:
                        fields[10 + offset],
                    Level2Title:
                        fields[11 + offset],
                    Level3Title:
                        fields[12 + offset],
                    Level4Title:
                        fields[13 + offset],
                    Level5Title:
                        fields[14 + offset],
                    LocationType:
                        ParseLocationType(
                            fields[15 + offset],
                            index),
                    MinimumSettlementClass:
                        ParseSettlementClass(
                            fields[16 + offset],
                            index),
                    RequiredOpportunityTags:
                        ParseTags(
                            fields[17 + offset])));
        }

        return result;
    }

    private static void Validate(
        IReadOnlyList<CareerDefinition> careers,
        IReadOnlySet<string> knownOpportunityTags)
    {
        if (careers.Count != 65)
        {
            throw new InvalidDataException(
                $"{DataPath} must contain exactly " +
                $"65 careers; found {careers.Count}.");
        }

        if (careers
            .Select(
                career =>
                    career.Id)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Count()
            != careers.Count)
        {
            throw new InvalidDataException(
                $"{DataPath} contains duplicate career IDs.");
        }

        var actualCareerIds =
            careers
                .Select(career => career.Id)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        if (!actualCareerIds.SetEquals(
                ExpectedCareerIds))
        {
            throw new InvalidDataException(
                $"{DataPath} must preserve the existing " +
                "65 career IDs for save compatibility.");
        }

        foreach (var career in careers)
        {
            if (string.IsNullOrWhiteSpace(
                career.Emoji))
            {
                throw new InvalidDataException(
                    $"{career.Name}: career emoji is required.");
            }

            if (career.StartYear
                < GameCalendarConfiguration.GameStartYear
                || career.StartYear
                    > CareerDefinition
                        .TechnologyFreezeYear)
            {
                throw new InvalidDataException(
                    $"{career.Name}: invalid start year " +
                    $"{career.StartYear}.");
            }

            if (career.EndYear is int endYear
                && endYear < career.StartYear)
            {
                throw new InvalidDataException(
                    $"{career.Name}: end year precedes " +
                    "start year.");
            }

            if (career.MaleEarly <= 0
                || career.FemaleEarly <= 0
                || career.MaleLate <= 0
                || career.FemaleLate <= 0)
            {
                throw new InvalidDataException(
                    $"{career.Name}: sex may influence " +
                    "career entry but may not make it " +
                    "impossible.");
            }

            if (career.LocationType
                    == CareerLocationType.Specialist
                && career.RequiredOpportunityTags.Count == 0)
            {
                throw new InvalidDataException(
                    $"{career.Name}: specialist careers require "
                    + "at least one opportunity tag.");
            }

            if (career.RequiredOpportunityTags
                .Any(tag =>
                    !knownOpportunityTags.Contains(tag)))
            {
                var unknown =
                    career.RequiredOpportunityTags
                        .First(tag =>
                            !knownOpportunityTags.Contains(tag));

                throw new InvalidDataException(
                    $"{career.Name}: unknown opportunity tag " +
                    $"'{unknown}'.");
            }

            if (career.LocationType
                    != CareerLocationType.Specialist
                && career.RequiredOpportunityTags.Count > 0)
            {
                throw new InvalidDataException(
                    $"{career.Name}: opportunity tags are only "
                    + "valid for specialist careers.");
            }

            if (career.BaseSalary <= 0)
            {
                throw new InvalidDataException(
                    $"{career.Name}: base salary must " +
                    "be positive.");
            }

            if (new[]
                {
                    career.Level1Title,
                    career.Level2Title,
                    career.Level3Title,
                    career.Level4Title,
                    career.Level5Title
                }
                .Any(
                    string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException(
                    $"{career.Name}: all five job titles " +
                    "are required.");
            }
        }
    }

    private static CareerLocationType ParseLocationType(
        string value,
        int rowIndex)
    {
        if (Enum.TryParse<CareerLocationType>(
                value,
                ignoreCase: true,
                out var result))
        {
            return result;
        }

        throw new InvalidDataException(
            $"{DataPath} row {rowIndex + 1}: "
            + $"'{value}' is not a valid location type.");
    }

    private static SettlementClass ParseSettlementClass(
        string value,
        int rowIndex)
    {
        if (Enum.TryParse<SettlementClass>(
                value,
                ignoreCase: true,
                out var result))
        {
            return result;
        }

        throw new InvalidDataException(
            $"{DataPath} row {rowIndex + 1}: "
            + $"'{value}' is not a valid settlement class.");
    }

    private static IReadOnlyList<string> ParseTags(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Trim() == "-")
        {
            return Array.Empty<string>();
        }

        return value
            .Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlySet<string>
        ParseOpportunityTags(
            string text)
    {
        var lines =
            text.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
        {
            throw new InvalidDataException(
                $"{OpportunityTagsPath} is empty.");
        }

        var result =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (var index = 1;
            index < lines.Length;
            index++)
        {
            var fields =
                lines[index].Split(',');

            if (fields.Length != 4
                || string.IsNullOrWhiteSpace(fields[0]))
            {
                throw new InvalidDataException(
                    $"Invalid {OpportunityTagsPath} row " +
                    $"{index + 1}.");
            }

            result.Add(
                fields[0].Trim());
        }

        return result;
    }

    private static int ParseInt(
        string value,
        int rowIndex)
    {
        if (int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result))
        {
            return result;
        }

        throw new InvalidDataException(
            $"{DataPath} row {rowIndex + 1}: " +
            $"'{value}' is not an integer.");
    }

    private static decimal ParseDecimal(
        string value,
        int rowIndex)
    {
        if (decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var result))
        {
            return result;
        }

        throw new InvalidDataException(
            $"{DataPath} row {rowIndex + 1}: " +
            $"'{value}' is not a decimal.");
    }

    private sealed record WeightedCareer(
        CareerDefinition Career,
        double Weight);
}
