using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal sealed class CareerEducationProfileCatalog
{
    private const string DataPath = "Career/career_education_profiles.csv";

    private readonly IReadOnlyDictionary<string, CareerEducationProfile> _profiles;

    private CareerEducationProfileCatalog(IReadOnlyDictionary<string, CareerEducationProfile> profiles)
    {
        _profiles = profiles;
    }

    public static CareerEducationProfileCatalog Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var text = data.ReadText(DataPath);
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "ProfileId,Level1Education,Level2Education,Level3Education,Level4Education,Level5Education";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{DataPath} has an unexpected header or is empty.");

        var result = new Dictionary<string, CareerEducationProfile>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 6)
                throw new InvalidDataException($"{DataPath} row {index + 1}: expected 6 fields.");

            var levels = fields.Skip(1)
                .Select(value => ParseLevel(value, index + 1))
                .ToArray();
            for (var level = 1; level < levels.Length; level++)
            {
                if (levels[level] < levels[level - 1])
                    throw new InvalidDataException($"{DataPath} row {index + 1}: education levels must be non-decreasing.");
            }

            var id = fields[0].Trim();
            if (string.IsNullOrWhiteSpace(id) || !result.TryAdd(id, new CareerEducationProfile(id, levels)))
                throw new InvalidDataException($"{DataPath} row {index + 1}: duplicate or empty profile ID '{id}'.");
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

    private static int ParseLevel(string value, int row)
    {
        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || parsed is < 0 or > 5)
        {
            throw new InvalidDataException($"{DataPath} row {row}: invalid education level '{value}'.");
        }
        return parsed;
    }

    private sealed record CareerEducationProfile(string Id, IReadOnlyList<int> Levels);
}
