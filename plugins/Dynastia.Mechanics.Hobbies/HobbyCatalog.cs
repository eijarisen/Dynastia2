using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Hobbies;

public sealed class HobbyCatalog
{
    private const string HobbiesPath = "Hobbies/hobbies.csv";
    private const string ContextPath = "Hobbies/hobby_context_weights.csv";
    private const string ThoughtsPath = "Hobbies/hobby_thoughts.json";

    private static readonly IReadOnlySet<string> TownPreferences =
        new HashSet<string>(["Universal", "Rural", "Urban"], StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> Stats =
        new HashSet<string>(["strength", "intellect", "appeal"], StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> Temperaments =
        new HashSet<string>(["Melancholic", "Phlegmatic", "Sanguine", "Choleric"], StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyDictionary<string, HobbyDefinition> _byId;
    private readonly IReadOnlyDictionary<string, HobbyThoughtSet> _thoughts;

    private HobbyCatalog(
        IReadOnlyList<HobbyDefinition> hobbies,
        IReadOnlyDictionary<string, HobbyThoughtSet> thoughts)
    {
        Hobbies = hobbies;
        _byId = hobbies.ToDictionary(hobby => hobby.Id, StringComparer.OrdinalIgnoreCase);
        _thoughts = thoughts;
    }

    public IReadOnlyList<HobbyDefinition> Hobbies { get; }

    public static HobbyCatalog Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var hobbies = ParseHobbies(data.ReadText(HobbiesPath));
        var thoughts = CatalogValidation.DeserializeJson<Dictionary<string, HobbyThoughtSet>>(
            data,
            ThoughtsPath,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Validate(hobbies, thoughts);
        ValidateContextRows(data.ReadText(ContextPath), hobbies);

        return new HobbyCatalog(
            hobbies,
            new Dictionary<string, HobbyThoughtSet>(thoughts, StringComparer.OrdinalIgnoreCase));
    }

    public HobbyDefinition? Find(string id) =>
        _byId.TryGetValue(id, out var hobby) ? hobby : null;

    public HobbyThoughtSet GetThoughts(string id) =>
        _thoughts.TryGetValue(id, out var thoughts)
            ? thoughts
            : throw CatalogValidation.Error(ThoughtsPath, "an entry for the requested hobby", item: id, field: "HobbyId", value: id);

    private static IReadOnlyList<HobbyDefinition> ParseHobbies(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string expectedHeader =
            "Id,Name,StartYear,EndYear,MinimumAge,BaseWeight,TownPreference,PrimaryStat,SecondaryStat,PrimaryTemperament,SecondaryTemperament,Emoji";

        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(expectedHeader, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                HobbiesPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                expectedHeader);
        }

        var result = new List<HobbyDefinition>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 12)
                throw CatalogValidation.FieldCount(HobbiesPath, row, fields.Length, 12);

            result.Add(new HobbyDefinition(
                fields[0].Trim(),
                fields[1].Trim(),
                CatalogValidation.ParseInt(HobbiesPath, row, "StartYear", fields[2]),
                string.IsNullOrWhiteSpace(fields[3]) ? (int?)null : CatalogValidation.ParseInt(HobbiesPath, row, "EndYear", fields[3]),
                CatalogValidation.ParseInt(HobbiesPath, row, "MinimumAge", fields[4]),
                CatalogValidation.ParseDouble(HobbiesPath, row, "BaseWeight", fields[5]),
                fields[6].Trim(),
                fields[7].Trim(),
                OptionalDash(fields[8]),
                fields[9].Trim(),
                Optional(fields[10]),
                fields[11].Trim()));
        }

        return result;
    }

    private static void Validate(
        IReadOnlyList<HobbyDefinition> hobbies,
        IReadOnlyDictionary<string, HobbyThoughtSet> thoughts)
    {
        if (hobbies.Count == 0)
            throw CatalogValidation.Error(HobbiesPath, "at least one hobby", field: "Root", value: hobbies.Count);

        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < hobbies.Count; index++)
        {
            var hobby = hobbies[index];
            var row = index + 2;
            var item = string.IsNullOrWhiteSpace(hobby.Id) ? $"row {row}" : hobby.Id;

            if (string.IsNullOrWhiteSpace(hobby.Id))
                throw CatalogValidation.Error(HobbiesPath, "a non-empty hobby ID", row, item, "Id", hobby.Id);
            if (string.IsNullOrWhiteSpace(hobby.Name))
                throw CatalogValidation.Error(HobbiesPath, "a non-empty display name", row, hobby.Id, "Name", hobby.Name);
            if (string.IsNullOrWhiteSpace(hobby.Emoji))
                throw CatalogValidation.Error(HobbiesPath, "a non-empty emoji", row, hobby.Id, "Emoji", hobby.Emoji);
            if (!ids.TryAdd(hobby.Id, row))
                throw CatalogValidation.Error(HobbiesPath, $"a unique ID; first defined at row {ids[hobby.Id]}", row, hobby.Id, "Id", hobby.Id);
            if (hobby.StartYear < GameCalendarConfiguration.GameStartYear)
                throw CatalogValidation.Error(HobbiesPath, $"a year at or after {GameCalendarConfiguration.GameStartYear}", row, hobby.Id, "StartYear", hobby.StartYear);
            if (hobby.EndYear is int endYear && endYear < hobby.StartYear)
                throw CatalogValidation.Error(HobbiesPath, $"a year at or after StartYear ({hobby.StartYear})", row, hobby.Id, "EndYear", endYear);
            if (hobby.MinimumAge < 5)
                throw CatalogValidation.Error(HobbiesPath, "an age of at least 5", row, hobby.Id, "MinimumAge", hobby.MinimumAge);
            if (hobby.BaseWeight <= 0)
                throw CatalogValidation.Error(HobbiesPath, "a number greater than 0", row, hobby.Id, "BaseWeight", hobby.BaseWeight);
            if (!TownPreferences.Contains(hobby.TownPreference))
                throw CatalogValidation.Error(HobbiesPath, $"one of: {string.Join(", ", TownPreferences)}", row, hobby.Id, "TownPreference", hobby.TownPreference);
            if (!Stats.Contains(hobby.PrimaryStat))
                throw CatalogValidation.Error(HobbiesPath, $"one of: {string.Join(", ", Stats)}", row, hobby.Id, "PrimaryStat", hobby.PrimaryStat);
            if (hobby.SecondaryStat is not null && !Stats.Contains(hobby.SecondaryStat))
                throw CatalogValidation.Error(HobbiesPath, $"one of: {string.Join(", ", Stats)}, or blank", row, hobby.Id, "SecondaryStat", hobby.SecondaryStat);
            if (hobby.SecondaryStat?.Equals(hobby.PrimaryStat, StringComparison.OrdinalIgnoreCase) == true)
                throw CatalogValidation.Error(HobbiesPath, "a stat different from PrimaryStat", row, hobby.Id, "SecondaryStat", hobby.SecondaryStat);
            if (!Temperaments.Contains(hobby.PrimaryTemperament))
                throw CatalogValidation.Error(HobbiesPath, $"one of: {string.Join(", ", Temperaments)}", row, hobby.Id, "PrimaryTemperament", hobby.PrimaryTemperament);
            if (hobby.SecondaryTemperament is not null && !Temperaments.Contains(hobby.SecondaryTemperament))
                throw CatalogValidation.Error(HobbiesPath, $"one of: {string.Join(", ", Temperaments)}, or blank", row, hobby.Id, "SecondaryTemperament", hobby.SecondaryTemperament);

            if (!thoughts.TryGetValue(hobby.Id, out var set))
                throw CatalogValidation.Error(ThoughtsPath, "an entry for every hobby ID", item: hobby.Id, field: "HobbyId", value: hobby.Id);
            ValidateThoughts(hobby, set);
        }

        var orphanThoughtId = thoughts.Keys.FirstOrDefault(id => !ids.ContainsKey(id));
        if (orphanThoughtId is not null)
            throw CatalogValidation.Error(ThoughtsPath, "a hobby ID defined in Hobbies/hobbies.csv", item: orphanThoughtId, field: "HobbyId", value: orphanThoughtId);
    }

    private static void ValidateContextRows(
        string text,
        IReadOnlyList<HobbyDefinition> hobbies)
    {
        var byId = hobbies.ToDictionary(h => h.Id, StringComparer.OrdinalIgnoreCase);
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "ItemId,StartYear,EndYear,Dimension,Value,WeightMultiplier";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                ContextPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var ageProfileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 6)
                throw CatalogValidation.FieldCount(ContextPath, row, fields.Length, 6);

            var id = fields[0].Trim();
            if (!byId.TryGetValue(id, out var hobby))
                throw CatalogValidation.Error(ContextPath, "a hobby ID defined in Hobbies/hobbies.csv", row, id, "ItemId", id);

            var start = CatalogValidation.ParseInt(ContextPath, row, "StartYear", fields[1]);
            var end = string.IsNullOrWhiteSpace(fields[2]) ? (int?)null : CatalogValidation.ParseInt(ContextPath, row, "EndYear", fields[2]);
            if (start < hobby.StartYear)
                throw CatalogValidation.Error(ContextPath, $"a year at or after hobby StartYear ({hobby.StartYear})", row, id, "StartYear", start);
            if (hobby.EndYear is int hobbyEnd && (end ?? int.MaxValue) > hobbyEnd)
                throw CatalogValidation.Error(ContextPath, $"a year no later than hobby EndYear ({hobbyEnd})", row, id, "EndYear", end);

            if (fields[3].Trim().Equals("AgeBand", StringComparison.OrdinalIgnoreCase))
                ageProfileIds.Add(id);
        }

        var missingAge = hobbies.FirstOrDefault(hobby => !ageProfileIds.Contains(hobby.Id));
        if (missingAge is not null)
            throw CatalogValidation.Error(ContextPath, "at least one AgeBand row for every hobby", item: missingAge.Id, field: "Dimension", value: "<missing>");
    }

    private static void ValidateThoughts(HobbyDefinition hobby, HobbyThoughtSet set)
    {
        if (set.AdultRough.Count == 0)
            throw CatalogValidation.Error(ThoughtsPath, "at least one adult rough thought", item: hobby.Id, field: "adultRough", value: set.AdultRough.Count);
        if (set.AdultNormal.Count == 0)
            throw CatalogValidation.Error(ThoughtsPath, "at least one adult normal thought", item: hobby.Id, field: "adultNormal", value: set.AdultNormal.Count);
        if (set.AdultElaborate.Count == 0)
            throw CatalogValidation.Error(ThoughtsPath, "at least one adult elaborate thought", item: hobby.Id, field: "adultElaborate", value: set.AdultElaborate.Count);
        if (hobby.MinimumAge <= 11 && set.Child.Count == 0)
            throw CatalogValidation.Error(ThoughtsPath, "at least one child thought for a hobby available to children", item: hobby.Id, field: "child", value: set.Child.Count);
        if (hobby.MinimumAge <= 17 && set.Adolescent.Count == 0)
            throw CatalogValidation.Error(ThoughtsPath, "at least one adolescent thought for a hobby available to adolescents", item: hobby.Id, field: "adolescent", value: set.Adolescent.Count);
    }

    private static string? Optional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? OptionalDash(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim() == "-" ? null : value.Trim();
}
