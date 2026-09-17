using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal sealed class CareerEducationProfileCatalog
{
    private const string DataPath = "Career/career_education_profiles.csv";

    private static readonly string[] LevelFields =
    [
        "Level1Education",
        "Level2Education",
        "Level3Education",
        "Level4Education",
        "Level5Education"
    ];

    private readonly IReadOnlyDictionary<string, CareerEducationProfile> _profiles;

    private CareerEducationProfileCatalog(
        IReadOnlyDictionary<string, CareerEducationProfile> profiles)
    {
        _profiles = profiles;
    }

    public static CareerEducationProfileCatalog Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var text = data.ReadText(DataPath);
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header =
            "ProfileId,Level1Education,Level2Education,Level3Education,Level4Education,Level5Education";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                DataPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var result = new Dictionary<string, CareerEducationProfile>(StringComparer.OrdinalIgnoreCase);
        var firstRows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 6)
                throw CatalogValidation.FieldCount(DataPath, row, fields.Length, 6);

            var id = fields[0].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a non-empty profile ID",
                    row,
                    field: "ProfileId",
                    value: id);
            }

            var levels = new int[5];
            for (var level = 0; level < levels.Length; level++)
            {
                var field = LevelFields[level];
                levels[level] = CatalogValidation.ParseInt(
                    DataPath,
                    row,
                    field,
                    fields[level + 1]);

                if (levels[level] is < 0 or > 5)
                {
                    throw CatalogValidation.Error(
                        DataPath,
                        "an education level from 0 through 5",
                        row,
                        id,
                        field,
                        levels[level]);
                }

                if (level > 0 && levels[level] < levels[level - 1])
                {
                    throw CatalogValidation.Error(
                        DataPath,
                        $"a value at least {LevelFields[level - 1]} ({levels[level - 1]})",
                        row,
                        id,
                        field,
                        levels[level]);
                }
            }

            if (!result.TryAdd(id, new CareerEducationProfile(id, levels)))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"a unique ProfileId; first defined at row {firstRows[id]}",
                    row,
                    id,
                    "ProfileId",
                    id);
            }

            firstRows[id] = row;
        }

        return new CareerEducationProfileCatalog(result);
    }

    public bool Contains(string id) => _profiles.ContainsKey(id);

    public int GetExpectedEducation(string profileId, int jobLevel)
    {
        if (!_profiles.TryGetValue(profileId, out var profile))
            throw new InvalidOperationException($"Unknown education profile '{profileId}'.");
        return profile.Levels[Math.Clamp(jobLevel, 1, 5) - 1];
    }

    private sealed record CareerEducationProfile(string Id, IReadOnlyList<int> Levels);
}
