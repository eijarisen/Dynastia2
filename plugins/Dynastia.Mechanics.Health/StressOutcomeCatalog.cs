using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class StressOutcomeCatalog
{
    private const string Path = "Health/health_stress_outcomes.csv";
    private readonly IReadOnlyList<StressOutcomeDefinition> _definitions;

    private StressOutcomeCatalog(IReadOnlyList<StressOutcomeDefinition> definitions)
    {
        _definitions = definitions;
    }

    public IReadOnlyList<StressOutcomeDefinition> Definitions => _definitions;

    public static StressOutcomeCatalog Load(
        IGameDataService data,
        IEnumerable<HealthConditionDefinition> conditions)
    {
        var known = conditions.ToDictionary(
            condition => condition.Id,
            StringComparer.OrdinalIgnoreCase);
        var lines = data.ReadText(Path)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header =
            "ConditionId,StartYear,EndYear,MinimumAge,MinimumStress,BaseWeight";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                Path,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var result = new List<StressOutcomeDefinition>();
        var firstRows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 6)
                throw CatalogValidation.FieldCount(Path, row, fields.Length, 6);

            var conditionId = fields[0].Trim();
            if (!known.TryGetValue(conditionId, out var condition))
            {
                throw CatalogValidation.Error(
                    Path,
                    "a ConditionId present in Common/health_conditions.json",
                    row,
                    field: "ConditionId",
                    value: conditionId);
            }

            if (condition.Weight != 0)
            {
                throw CatalogValidation.Error(
                    Path,
                    "a condition whose ordinary selection weight is 0",
                    row,
                    conditionId,
                    "ConditionId",
                    conditionId);
            }

            if (firstRows.TryGetValue(conditionId, out var firstRow))
            {
                throw CatalogValidation.Error(
                    Path,
                    $"a unique ConditionId; first defined at row {firstRow}",
                    row,
                    conditionId,
                    "ConditionId",
                    conditionId);
            }
            firstRows[conditionId] = row;

            var start = CatalogValidation.ParseInt(Path, row, "StartYear", fields[1]);
            var end = string.IsNullOrWhiteSpace(fields[2])
                ? (int?)null
                : CatalogValidation.ParseInt(Path, row, "EndYear", fields[2]);
            var minimumAge = CatalogValidation.ParseInt(Path, row, "MinimumAge", fields[3]);
            var minimumStress = CatalogValidation.ParseDouble(Path, row, "MinimumStress", fields[4]);
            var baseWeight = CatalogValidation.ParseDouble(Path, row, "BaseWeight", fields[5]);

            if (start < GameCalendarConfiguration.GameStartYear)
            {
                throw CatalogValidation.Error(
                    Path,
                    $"a year at or after {GameCalendarConfiguration.GameStartYear}",
                    row,
                    conditionId,
                    "StartYear",
                    start);
            }

            if (end is int endYear && endYear < start)
            {
                throw CatalogValidation.Error(
                    Path,
                    $"a year at or after StartYear ({start})",
                    row,
                    conditionId,
                    "EndYear",
                    endYear);
            }

            if (minimumAge < 0)
            {
                throw CatalogValidation.Error(
                    Path,
                    "an age greater than or equal to 0",
                    row,
                    conditionId,
                    "MinimumAge",
                    minimumAge);
            }

            if (minimumStress < 0)
            {
                throw CatalogValidation.Error(
                    Path,
                    "a number greater than or equal to 0",
                    row,
                    conditionId,
                    "MinimumStress",
                    minimumStress);
            }

            if (baseWeight <= 0)
            {
                throw CatalogValidation.Error(
                    Path,
                    "a number greater than 0",
                    row,
                    conditionId,
                    "BaseWeight",
                    baseWeight);
            }

            result.Add(new StressOutcomeDefinition(
                conditionId,
                start,
                end,
                minimumAge,
                minimumStress,
                baseWeight));
        }

        return new StressOutcomeCatalog(result);
    }
}
