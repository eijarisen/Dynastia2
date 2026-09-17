using System.Globalization;
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
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{DataPath} has an unexpected header or is empty.");

        var result = new List<CareerDefinition>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 23)
                throw new InvalidDataException($"{DataPath} row {index + 1} has {fields.Length} fields; expected 23.");

            result.Add(new CareerDefinition(
                Id: fields[1].Trim(),
                Name: fields[2].Trim(),
                Emoji: fields[3].Trim(),
                StartYear: ParseInt(fields[4], index),
                EndYear: string.IsNullOrWhiteSpace(fields[5]) ? null : ParseInt(fields[5], index),
                MaleEarly: ParseInt(fields[6], index),
                FemaleEarly: ParseInt(fields[7], index),
                MaleLate: ParseInt(fields[8], index),
                FemaleLate: ParseInt(fields[9], index),
                BaseSalary: ParseDecimal(fields[10], index),
                Level1Title: fields[11].Trim(),
                Level2Title: fields[12].Trim(),
                Level3Title: fields[13].Trim(),
                Level4Title: fields[14].Trim(),
                Level5Title: fields[15].Trim(),
                LocationType: ParseLocationType(fields[16], index),
                MinimumSettlementClass: ParseSettlementClass(fields[17], index),
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
            throw new InvalidDataException($"{DataPath} is empty.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var career in careers)
        {
            if (string.IsNullOrWhiteSpace(career.Id) || !ids.Add(career.Id))
                throw new InvalidDataException($"{DataPath} contains a duplicate or empty career ID '{career.Id}'.");
        }


        foreach (var career in careers)
        {
            if (string.IsNullOrWhiteSpace(career.Name) || string.IsNullOrWhiteSpace(career.Emoji))
                throw new InvalidDataException($"Career '{career.Id}' needs a name and emoji.");
            if (career.StartYear < GameCalendarConfiguration.GameStartYear
                || career.StartYear > CareerDefinition.TechnologyFreezeYear)
                throw new InvalidDataException($"{career.Name}: invalid start year {career.StartYear}.");
            if (career.EndYear is int endYear && endYear < career.StartYear)
                throw new InvalidDataException($"{career.Name}: end year precedes start year.");
            if (career.MaleEarly <= 0 || career.FemaleEarly <= 0 || career.MaleLate <= 0 || career.FemaleLate <= 0)
                throw new InvalidDataException($"{career.Name}: sex weighting must remain positive.");
            if (career.BaseSalary <= 0)
                throw new InvalidDataException($"{career.Name}: base salary must be positive.");
            if (!AllowedAptitudeStats.Contains(career.PrimaryStat))
                throw new InvalidDataException($"{career.Name}: invalid primary stat '{career.PrimaryStat}'.");
            if (career.SecondaryStat is not null
                && (!AllowedAptitudeStats.Contains(career.SecondaryStat)
                    || career.SecondaryStat.Equals(career.PrimaryStat, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"{career.Name}: invalid secondary stat '{career.SecondaryStat}'.");
            if (!educationProfiles.Contains(career.EducationProfile))
                throw new InvalidDataException($"{career.Name}: unknown education profile '{career.EducationProfile}'.");
            if (string.IsNullOrWhiteSpace(career.CareerFamily))
                throw new InvalidDataException($"{career.Name}: CareerFamily is required.");

            if (career.LocationType == CareerLocationType.Specialist && career.RequiredOpportunityTags.Count == 0)
                throw new InvalidDataException($"{career.Name}: specialist careers require at least one opportunity tag.");
            if (career.RequiredOpportunityTags.Any(tag => !knownOpportunityTags.Contains(tag)))
            {
                var unknown = career.RequiredOpportunityTags.First(tag => !knownOpportunityTags.Contains(tag));
                throw new InvalidDataException($"{career.Name}: unknown opportunity tag '{unknown}'.");
            }
            if (career.LocationType != CareerLocationType.Specialist && career.RequiredOpportunityTags.Count > 0)
                throw new InvalidDataException($"{career.Name}: opportunity tags are only valid for specialist careers.");

            if (new[] { career.Level1Title, career.Level2Title, career.Level3Title, career.Level4Title, career.Level5Title }
                .Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"{career.Name}: all five job titles are required.");
        }
    }

    private static string? NormalizeOptional(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) || trimmed == "-" ? null : trimmed;
    }

    private static CareerLocationType ParseLocationType(string value, int rowIndex)
    {
        if (Enum.TryParse<CareerLocationType>(value, true, out var result))
            return result;
        throw new InvalidDataException($"{DataPath} row {rowIndex + 1}: '{value}' is not a valid location type.");
    }

    private static SettlementClass ParseSettlementClass(string value, int rowIndex)
    {
        if (Enum.TryParse<SettlementClass>(value, true, out var result))
            return result;
        throw new InvalidDataException($"{DataPath} row {rowIndex + 1}: '{value}' is not a valid settlement class.");
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
        if (lines.Length < 2)
            throw new InvalidDataException($"{OpportunityTagsPath} is empty.");

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 4 || string.IsNullOrWhiteSpace(fields[0]))
                throw new InvalidDataException($"Invalid {OpportunityTagsPath} row {index + 1}.");
            result.Add(fields[0].Trim());
        }
        return result;
    }

    private static int ParseInt(string value, int rowIndex)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new InvalidDataException($"{DataPath} row {rowIndex + 1}: '{value}' is not an integer.");
    }

    private static decimal ParseDecimal(string value, int rowIndex)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new InvalidDataException($"{DataPath} row {rowIndex + 1}: '{value}' is not a decimal.");
    }

    private sealed record WeightedCareer(CareerDefinition Career, double Weight);
}
