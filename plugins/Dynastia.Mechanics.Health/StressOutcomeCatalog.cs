using System.Globalization;
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
        var known = conditions.ToDictionary(condition => condition.Id, StringComparer.OrdinalIgnoreCase);
        var lines = data.ReadText(Path).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "ConditionId,StartYear,EndYear,MinimumAge,MinimumStress,BaseWeight";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{Path} has an unexpected header or is empty.");

        var result = new List<StressOutcomeDefinition>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 6)
                throw new InvalidDataException($"{Path} row {row}: expected 6 fields.");

            var conditionId = fields[0].Trim();
            if (!known.TryGetValue(conditionId, out var condition))
                throw new InvalidDataException($"{Path} row {row} ConditionId: unknown condition '{conditionId}'.");
            if (condition.Weight != 0)
                throw new InvalidDataException($"{Path} row {row} ConditionId: stress outcome '{conditionId}' must have ordinary selection weight 0.");

            var start = ParseInt(fields[1], row, "StartYear");
            var end = string.IsNullOrWhiteSpace(fields[2]) ? null : ParseInt(fields[2], row, "EndYear");
            var minimumAge = ParseInt(fields[3], row, "MinimumAge");
            var minimumStress = ParseDouble(fields[4], row, "MinimumStress");
            var baseWeight = ParseDouble(fields[5], row, "BaseWeight");

            if (start < GameCalendarConfiguration.GameStartYear)
                throw new InvalidDataException($"{Path} row {row} StartYear: may not precede {GameCalendarConfiguration.GameStartYear}.");
            if (end is int endYear && endYear < start)
                throw new InvalidDataException($"{Path} row {row} EndYear: may not precede StartYear.");
            if (minimumAge < 0 || minimumStress < 0 || baseWeight <= 0)
                throw new InvalidDataException($"{Path} row {row}: age/stress must be nonnegative and BaseWeight positive.");

            result.Add(new StressOutcomeDefinition(conditionId, start, end, minimumAge, minimumStress, baseWeight));
        }

        if (result.Select(definition => definition.ConditionId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Count)
            throw new InvalidDataException($"{Path}: duplicate condition IDs are not allowed.");

        return new StressOutcomeCatalog(result);
    }

    private static int ParseInt(string value, int row, string field)
    {
        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"{Path} row {row} {field}: invalid integer '{value}'.");
        return parsed;
    }

    private static double ParseDouble(string value, int row, string field)
    {
        if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"{Path} row {row} {field}: invalid number '{value}'.");
        return parsed;
    }
}
