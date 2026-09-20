using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Education;

public sealed class EducationLocalityRules
{
    private const string DataPath = "TownLife/education_locality_rules.json";

    private readonly IReadOnlyDictionary<int, int> _schoolTierMaximums;

    private EducationLocalityRules(
        IReadOnlyDictionary<int, int> schoolTierMaximums)
    {
        _schoolTierMaximums = schoolTierMaximums;
    }

    public static EducationLocalityRules Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var root = JsonSerializer.Deserialize<Root>(
            data.ReadText(DataPath),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })
            ?? throw CatalogValidation.Error(
                DataPath,
                "valid education-locality rules",
                field: "Root");

        var maximums = new Dictionary<int, int>();
        foreach (var pair in root.SchoolTierToMaximumLocalEducation)
        {
            if (!int.TryParse(pair.Key, out var tier)
                || tier is < 0 or > 5)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "school tier keys from 0 through 5",
                    field: "schoolTierToMaximumLocalEducation",
                    value: pair.Key);
            }

            if (pair.Value is < 0 or > 5)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "education levels from 0 through 5",
                    field: $"schoolTierToMaximumLocalEducation.{pair.Key}",
                    value: pair.Value);
            }

            maximums[tier] = pair.Value;
        }

        foreach (var tier in Enumerable.Range(0, 6))
        {
            if (!maximums.ContainsKey(tier))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "an entry for every school tier from 0 through 5",
                    field: "schoolTierToMaximumLocalEducation",
                    value: tier);
            }
        }

        return new EducationLocalityRules(maximums);
    }

    public int GetMaximumLocalEducation(int schoolTier) =>
        _schoolTierMaximums[Math.Clamp(schoolTier, 0, 5)];

    public EducationGenerationRange GetGeneratedAdultRange(
        EducationEraRule era,
        int schoolTier)
    {
        ArgumentNullException.ThrowIfNull(era);

        var maximum = Math.Min(
            era.GeneratedAdultMaxLevel,
            GetMaximumLocalEducation(schoolTier));
        var minimum = Math.Min(
            era.GeneratedAdultMinLevel,
            maximum);

        return new EducationGenerationRange(minimum, maximum);
    }

    private sealed class Root
    {
        public Dictionary<string, int> SchoolTierToMaximumLocalEducation { get; set; } = [];
    }
}
