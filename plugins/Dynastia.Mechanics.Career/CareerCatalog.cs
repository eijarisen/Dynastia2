using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal sealed class CareerCatalog
{
    private const string DataPath = "Career/careers.csv";
    private const string OpportunityTagsPath = "Towns/opportunity_tags.csv";

    private static readonly IReadOnlySet<string> AllowedAptitudeStats =
        new HashSet<string>(["strength", "intellect", "appeal"], StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyList<CareerDefinition> _careers;
    private readonly IReadOnlyDictionary<string, CareerDefinition> _byId;

    private CareerCatalog(IReadOnlyList<CareerDefinition> careers)
    {
        _careers = careers;
        _byId = careers.ToDictionary(career => career.Id, StringComparer.OrdinalIgnoreCase);
    }

    internal IReadOnlyCollection<string> CareerIds => _byId.Keys.ToArray();
    internal IReadOnlyCollection<string> CareerFamilies =>
        _careers.Select(career => career.CareerFamily)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    internal IReadOnlyList<CareerDefinition> All => _careers;

    public static CareerCatalog Load(IGameDataService data, CareerEducationProfileCatalog educationProfiles)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(educationProfiles);

        var careers = Parse(data.ReadText(DataPath));
        var knownOpportunityTags = ParseOpportunityTags(data.ReadText(OpportunityTagsPath));
        Validate(careers, knownOpportunityTags, educationProfiles);
        return new CareerCatalog(careers);
    }

    public CareerDefinition? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        return _byId.TryGetValue(id, out var career) ? career : null;
    }

    public CareerDefinition SelectForEntry(
        Sex sex,
        int gameYear,
        IGameRandom random,
        Func<CareerDefinition, double>? availabilityMultiplier = null)
    {
        var available = _careers
            .Where(career => career.IsOpenForEntry(gameYear))
            .Select(career =>
            {
                var multiplier = availabilityMultiplier?.Invoke(career) ?? 1.0;
                return new WeightedCareer(
                    career,
                    career.GetEntryWeight(sex, gameYear) * Math.Max(0, multiplier));
            })
            .Where(entry => entry.Weight > 0)
            .ToList();

        if (available.Count == 0)
            throw new InvalidOperationException($"No career is available for entry in year {gameYear}.");

        var total = available.Sum(entry => entry.Weight);
        var roll = random.NextDouble() * total;
        foreach (var entry in available)
        {
            if (roll < entry.Weight)
                return entry.Career;
            roll -= entry.Weight;
        }
        return available[^1].Career;
    }

    private static IReadOnlyList<CareerDefinition> Parse(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "Number,Id,Name,Emoji,StartYear,EndYear,MaleEarly,FemaleEarly,MaleLate,FemaleLate,BaseSalary,Level1Title,Level2Title,Level3Title,Level4Title,Level5Title,LocationType,MinimumSettlementClass,RequiredOpportunityTags,PrimaryStat,SecondaryStat,EducationProfile,CareerFamily";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                DataPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var result = new List<CareerDefinition>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 23)
                throw CatalogValidation.FieldCount(DataPath, row, fields.Length, 23);

            result.Add(new CareerDefinition(
                Id: fields[1].Trim(),
                Name: fields[2].Trim(),
                Emoji: fields[3].Trim(),
                StartYear: CatalogValidation.ParseInt(DataPath, row, "StartYear", fields[4]),
                EndYear: string.IsNullOrWhiteSpace(fields[5])
                    ? null
                    : CatalogValidation.ParseInt(DataPath, row, "EndYear", fields[5]),
                MaleEarly: CatalogValidation.ParseInt(DataPath, row, "MaleEarly", fields[6]),
                FemaleEarly: CatalogValidation.ParseInt(DataPath, row, "FemaleEarly", fields[7]),
                MaleLate: CatalogValidation.ParseInt(DataPath, row, "MaleLate", fields[8]),
                FemaleLate: CatalogValidation.ParseInt(DataPath, row, "FemaleLate", fields[9]),
                BaseSalary: CatalogValidation.ParseDecimal(DataPath, row, "BaseSalary", fields[10]),
                Level1Title: fields[11].Trim(),
                Level2Title: fields[12].Trim(),
                Level3Title: fields[13].Trim(),
                Level4Title: fields[14].Trim(),
                Level5Title: fields[15].Trim(),
                LocationType: CatalogValidation.ParseEnum<CareerLocationType>(
                    DataPath, row, "LocationType", fields[16]),
                MinimumSettlementClass: CatalogValidation.ParseEnum<SettlementClass>(
                    DataPath, row, "MinimumSettlementClass", fields[17]),
                RequiredOpportunityTags: ParseTags(fields[18]),
                PrimaryStat: fields[19].Trim(),
                SecondaryStat: NormalizeOptional(fields[20]),
                EducationProfile: fields[21].Trim(),
                CareerFamily: fields[22].Trim()));
        }
        return result;
    }

    private static void Validate(
        IReadOnlyList<CareerDefinition> careers,
        IReadOnlySet<string> knownOpportunityTags,
        CareerEducationProfileCatalog educationProfiles)
    {
        if (careers.Count == 0)
        {
            throw CatalogValidation.Error(
                DataPath,
                "at least one career row",
                field: "Rows",
                value: 0);
        }

        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < careers.Count; index++)
        {
            var career = careers[index];
            var row = index + 2;

            if (string.IsNullOrWhiteSpace(career.Id))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a non-empty career ID",
                    row,
                    field: "Id",
                    value: career.Id);
            }

            if (!ids.TryAdd(career.Id, row))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"a unique career ID; first defined at row {ids[career.Id]}",
                    row,
                    career.Id,
                    "Id",
                    career.Id);
            }

            if (string.IsNullOrWhiteSpace(career.Name))
                throw CatalogValidation.Error(DataPath, "a non-empty name", row, career.Id, "Name", career.Name);
            if (string.IsNullOrWhiteSpace(career.Emoji))
                throw CatalogValidation.Error(DataPath, "a non-empty emoji", row, career.Id, "Emoji", career.Emoji);

            if (career.StartYear < GameCalendarConfiguration.GameStartYear
                || career.StartYear > CareerDefinition.TechnologyFreezeYear)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"a year from {GameCalendarConfiguration.GameStartYear} through {CareerDefinition.TechnologyFreezeYear}",
                    row,
                    career.Id,
                    "StartYear",
                    career.StartYear);
            }

            if (career.EndYear is int endYear && endYear < career.StartYear)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"a year at or after StartYear ({career.StartYear})",
                    row,
                    career.Id,
                    "EndYear",
                    endYear);
            }

            ValidatePositiveWeight(row, career.Id, "MaleEarly", career.MaleEarly);
            ValidatePositiveWeight(row, career.Id, "FemaleEarly", career.FemaleEarly);
            ValidatePositiveWeight(row, career.Id, "MaleLate", career.MaleLate);
            ValidatePositiveWeight(row, career.Id, "FemaleLate", career.FemaleLate);

            if (career.BaseSalary <= 0)
                throw CatalogValidation.Error(DataPath, "a number greater than 0", row, career.Id, "BaseSalary", career.BaseSalary);

            if (!AllowedAptitudeStats.Contains(career.PrimaryStat))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"one of: {string.Join(", ", AllowedAptitudeStats.OrderBy(value => value))}",
                    row,
                    career.Id,
                    "PrimaryStat",
                    career.PrimaryStat);
            }

            if (career.SecondaryStat is not null
                && !AllowedAptitudeStats.Contains(career.SecondaryStat))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"one of: {string.Join(", ", AllowedAptitudeStats.OrderBy(value => value))}, or empty",
                    row,
                    career.Id,
                    "SecondaryStat",
                    career.SecondaryStat);
            }

            if (career.SecondaryStat is not null
                && career.SecondaryStat.Equals(career.PrimaryStat, StringComparison.OrdinalIgnoreCase))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a stat different from PrimaryStat",
                    row,
                    career.Id,
                    "SecondaryStat",
                    career.SecondaryStat);
            }

            if (!educationProfiles.Contains(career.EducationProfile))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "an EducationProfile defined in career_education_profiles.csv",
                    row,
                    career.Id,
                    "EducationProfile",
                    career.EducationProfile);
            }

            if (string.IsNullOrWhiteSpace(career.CareerFamily))
                throw CatalogValidation.Error(DataPath, "a non-empty career family", row, career.Id, "CareerFamily", career.CareerFamily);

            if (career.LocationType == CareerLocationType.Specialist
                && career.RequiredOpportunityTags.Count == 0)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "at least one opportunity tag for Specialist careers",
                    row,
                    career.Id,
                    "RequiredOpportunityTags",
                    string.Empty);
            }

            var unknownTag = career.RequiredOpportunityTags
                .FirstOrDefault(tag => !knownOpportunityTags.Contains(tag));
            if (unknownTag is not null)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "opportunity tags defined in Towns/opportunity_tags.csv",
                    row,
                    career.Id,
                    "RequiredOpportunityTags",
                    unknownTag);
            }

            if (career.LocationType != CareerLocationType.Specialist
                && career.RequiredOpportunityTags.Count > 0)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "an empty value unless LocationType is Specialist",
                    row,
                    career.Id,
                    "RequiredOpportunityTags",
                    string.Join(';', career.RequiredOpportunityTags));
            }

            var titles = new[]
            {
                ("Level1Title", career.Level1Title),
                ("Level2Title", career.Level2Title),
                ("Level3Title", career.Level3Title),
                ("Level4Title", career.Level4Title),
                ("Level5Title", career.Level5Title)
            };
            var missingTitle = titles.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.Item2));
            if (missingTitle != default)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a non-empty job title",
                    row,
                    career.Id,
                    missingTitle.Item1,
                    missingTitle.Item2);
            }
        }
    }

    private static void ValidatePositiveWeight(
        int row,
        string item,
        string field,
        int value)
    {
        if (value <= 0)
            throw CatalogValidation.Error(DataPath, "an integer greater than 0", row, item, field, value);
    }

    private static string? NormalizeOptional(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) || trimmed == "-" ? null : trimmed;
    }

    private static IReadOnlyList<string> ParseTags(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "-")
            return Array.Empty<string>();
        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlySet<string> ParseOpportunityTags(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "Tag,DisplayName,DefaultStartYear,EndYear";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                OpportunityTagsPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 4)
                throw CatalogValidation.FieldCount(OpportunityTagsPath, row, fields.Length, 4);

            var tag = fields[0].Trim();
            if (string.IsNullOrWhiteSpace(tag))
                throw CatalogValidation.Error(OpportunityTagsPath, "a non-empty tag ID", row, field: "Tag", value: tag);
            if (!result.Add(tag))
                throw CatalogValidation.Error(OpportunityTagsPath, "a unique tag ID", row, tag, "Tag", tag);
        }
        return result;
    }

    private sealed record WeightedCareer(CareerDefinition Career, double Weight);
}
