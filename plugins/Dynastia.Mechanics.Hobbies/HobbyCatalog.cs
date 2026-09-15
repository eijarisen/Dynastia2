using System.Globalization;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Hobbies;

public sealed class HobbyCatalog
{
    private const string HobbiesPath =
        "Hobbies/hobbies.csv";

    private const string ThoughtsPath =
        "Hobbies/hobby_thoughts.json";

    private static readonly IReadOnlySet<string> TownPreferences =
        new HashSet<string>(
            ["Universal", "Rural", "Urban"],
            StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> GenderPreferences =
        new HashSet<string>(
            [
                "Neutral",
                "Male-leaning",
                "Strongly male-leaning",
                "Female-leaning",
                "Strongly female-leaning"
            ],
            StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> Temperaments =
        new HashSet<string>(
            ["Melancholic", "Phlegmatic", "Sanguine", "Choleric"],
            StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyDictionary<string, HobbyDefinition> _byId;
    private readonly IReadOnlyDictionary<string, HobbyThoughtSet> _thoughts;

    private HobbyCatalog(
        IReadOnlyList<HobbyDefinition> hobbies,
        IReadOnlyDictionary<string, HobbyThoughtSet> thoughts)
    {
        Hobbies = hobbies;
        _byId = hobbies.ToDictionary(
            hobby => hobby.Id,
            StringComparer.OrdinalIgnoreCase);
        _thoughts = thoughts;
    }

    public IReadOnlyList<HobbyDefinition> Hobbies { get; }

    public static HobbyCatalog Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var hobbies = ParseHobbies(
            data.ReadText(HobbiesPath));

        var thoughts = JsonSerializer.Deserialize<
                Dictionary<string, HobbyThoughtSet>>(
                data.ReadText(ThoughtsPath),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? throw new InvalidDataException(
                $"{ThoughtsPath} contains no data.");

        Validate(hobbies, thoughts);

        return new HobbyCatalog(
            hobbies,
            new Dictionary<string, HobbyThoughtSet>(
                thoughts,
                StringComparer.OrdinalIgnoreCase));
    }

    public HobbyDefinition? Find(
        string id) =>
        _byId.TryGetValue(id, out var hobby)
            ? hobby
            : null;

    public HobbyThoughtSet GetThoughts(
        string id) =>
        _thoughts.TryGetValue(id, out var thoughts)
            ? thoughts
            : throw new InvalidDataException(
                $"No thought catalogue exists for hobby '{id}'.");

    private static IReadOnlyList<HobbyDefinition> ParseHobbies(
        string text)
    {
        var lines = text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);

        const string expectedHeader =
            "Id,Name,StartYear,EndYear,MinimumAge,TownPreference,GenderPreference,PrimaryTemperament,SecondaryTemperament,Emoji";

        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(
                expectedHeader,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{HobbiesPath} has an unexpected header or is empty.");
        }

        var result = new List<HobbyDefinition>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 10)
            {
                throw new InvalidDataException(
                    $"Invalid {HobbiesPath} row {index + 1}: expected 10 fields.");
            }

            result.Add(
                new HobbyDefinition(
                    fields[0].Trim(),
                    fields[1].Trim(),
                    ParseInt(fields[2], HobbiesPath, index),
                    ParseOptionalInt(fields[3], HobbiesPath, index),
                    ParseInt(fields[4], HobbiesPath, index),
                    fields[5].Trim(),
                    fields[6].Trim(),
                    fields[7].Trim(),
                    Optional(fields[8]),
                    fields[9].Trim()));
        }

        return result;
    }

    private static void Validate(
        IReadOnlyList<HobbyDefinition> hobbies,
        IReadOnlyDictionary<string, HobbyThoughtSet> thoughts)
    {
        if (hobbies.Count == 0)
            throw new InvalidDataException($"{HobbiesPath} contains no hobbies.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hobby in hobbies)
        {
            if (string.IsNullOrWhiteSpace(hobby.Id)
                || string.IsNullOrWhiteSpace(hobby.Name)
                || string.IsNullOrWhiteSpace(hobby.Emoji))
            {
                throw new InvalidDataException(
                    $"{HobbiesPath} contains an incomplete hobby definition.");
            }

            if (!ids.Add(hobby.Id))
                throw new InvalidDataException($"Duplicate hobby ID '{hobby.Id}'.");

            if (hobby.StartYear < GameCalendarConfiguration.GameStartYear)
                throw new InvalidDataException($"Hobby '{hobby.Id}' begins before the supported game era.");

            if (hobby.EndYear is int endYear && endYear < hobby.StartYear)
                throw new InvalidDataException($"Hobby '{hobby.Id}' ends before it begins.");

            if (hobby.MinimumAge < 5)
                throw new InvalidDataException($"Hobby '{hobby.Id}' has a minimum age below five.");

            if (!TownPreferences.Contains(hobby.TownPreference))
                throw new InvalidDataException($"Hobby '{hobby.Id}' has unknown town preference '{hobby.TownPreference}'.");

            if (!GenderPreferences.Contains(hobby.GenderPreference))
                throw new InvalidDataException($"Hobby '{hobby.Id}' has unknown gender preference '{hobby.GenderPreference}'.");

            if (!Temperaments.Contains(hobby.PrimaryTemperament)
                || (hobby.SecondaryTemperament is not null
                    && !Temperaments.Contains(hobby.SecondaryTemperament)))
            {
                throw new InvalidDataException($"Hobby '{hobby.Id}' has an unknown temperament preference.");
            }

            if (!thoughts.TryGetValue(hobby.Id, out var set))
                throw new InvalidDataException($"{ThoughtsPath} is missing hobby '{hobby.Id}'.");

            ValidateThoughts(hobby, set);
        }

        var orphanThoughtIds = thoughts.Keys
            .Where(id => !ids.Contains(id))
            .ToList();

        if (orphanThoughtIds.Count > 0)
        {
            throw new InvalidDataException(
                $"{ThoughtsPath} contains unknown hobby '{orphanThoughtIds[0]}'.");
        }
    }

    private static void ValidateThoughts(
        HobbyDefinition hobby,
        HobbyThoughtSet set)
    {
        if (set.AdultRough.Count == 0
            || set.AdultNormal.Count == 0
            || set.AdultElaborate.Count == 0)
        {
            throw new InvalidDataException(
                $"Hobby '{hobby.Id}' is missing adult thought wording.");
        }

        if (hobby.MinimumAge <= 11 && set.Child.Count == 0)
        {
            throw new InvalidDataException(
                $"Hobby '{hobby.Id}' is missing child thought wording.");
        }

        if (hobby.MinimumAge <= 17 && set.Adolescent.Count == 0)
        {
            throw new InvalidDataException(
                $"Hobby '{hobby.Id}' is missing adolescent thought wording.");
        }
    }

    private static int ParseInt(
        string value,
        string path,
        int rowIndex)
    {
        if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            throw new InvalidDataException(
                $"Invalid integer in {path} row {rowIndex + 1}.");
        }

        return parsed;
    }

    private static int? ParseOptionalInt(
        string value,
        string path,
        int rowIndex) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : ParseInt(value, path, rowIndex);

    private static string? Optional(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
