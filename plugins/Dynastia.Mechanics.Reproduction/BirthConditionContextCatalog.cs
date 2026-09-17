using Dynastia.Contracts;

namespace Dynastia.Mechanics.Reproduction;

public sealed class BirthConditionContextCatalog
{
    private const string Path = "Health/birth_condition_context_weights.csv";
    private readonly IReadOnlyDictionary<string, IReadOnlyList<Rule>> _rules;

    private BirthConditionContextCatalog(IReadOnlyList<Rule> rules)
    {
        _rules = rules.GroupBy(rule => rule.ConditionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Rule>)group.ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    public static BirthConditionContextCatalog Load(
        IGameDataService data,
        IEnumerable<string> knownConditionIds)
    {
        var known = knownConditionIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lines = data.ReadText(Path)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header =
            "ConditionId,StartYear,EndYear,MotherAgeBand,WeightMultiplier";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                Path,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var rows = new List<Rule>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 5)
                throw CatalogValidation.FieldCount(Path, row, fields.Length, 5);

            var id = fields[0].Trim();
            if (!known.Contains(id))
            {
                throw CatalogValidation.Error(
                    Path,
                    "a ConditionId present in Common/birth_conditions.json",
                    row,
                    field: "ConditionId",
                    value: id);
            }

            var start = CatalogValidation.ParseInt(Path, row, "StartYear", fields[1]);
            var end = string.IsNullOrWhiteSpace(fields[2])
                ? (int?)null
                : CatalogValidation.ParseInt(Path, row, "EndYear", fields[2]);
            var ageBand = fields[3].Trim();
            var multiplier = CatalogValidation.ParseDouble(
                Path,
                row,
                "WeightMultiplier",
                fields[4]);

            if (start < GameCalendarConfiguration.GameStartYear)
            {
                throw CatalogValidation.Error(
                    Path,
                    $"a year at or after {GameCalendarConfiguration.GameStartYear}",
                    row,
                    id,
                    "StartYear",
                    start);
            }

            if (end is int endYear && endYear < start)
            {
                throw CatalogValidation.Error(
                    Path,
                    $"a year at or after StartYear ({start})",
                    row,
                    id,
                    "EndYear",
                    endYear);
            }

            if (ageBand is not ("YoungAdult" or "Adult"))
            {
                throw CatalogValidation.Error(
                    Path,
                    "YoungAdult or Adult",
                    row,
                    id,
                    "MotherAgeBand",
                    ageBand);
            }

            if (multiplier <= 0)
            {
                throw CatalogValidation.Error(
                    Path,
                    "a number greater than 0",
                    row,
                    id,
                    "WeightMultiplier",
                    multiplier);
            }

            rows.Add(new Rule(row, id, start, end, ageBand, multiplier));
        }

        foreach (var group in rows.GroupBy(
                     rule => (
                         rule.ConditionId.ToUpperInvariant(),
                         rule.MotherAgeBand.ToUpperInvariant())))
        {
            var ordered = group.OrderBy(rule => rule.StartYear).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                if (previous.EndYear is null
                    || previous.EndYear.Value >= current.StartYear)
                {
                    throw CatalogValidation.Error(
                        Path,
                        $"a year range that does not overlap row {previous.SourceRow}",
                        current.SourceRow,
                        current.ConditionId,
                        "StartYear",
                        current.StartYear);
                }
            }
        }

        return new BirthConditionContextCatalog(rows);
    }

    public double GetMultiplier(string conditionId, int year, int motherAge)
    {
        if (!_rules.TryGetValue(conditionId, out var rules))
            return 1.0;

        var ageBand = motherAge <= 34 ? "YoungAdult" : "Adult";
        return rules.LastOrDefault(rule =>
            rule.MotherAgeBand.Equals(ageBand, StringComparison.OrdinalIgnoreCase)
            && rule.Covers(year))?.WeightMultiplier ?? 1.0;
    }

    private sealed record Rule(
        int SourceRow,
        string ConditionId,
        int StartYear,
        int? EndYear,
        string MotherAgeBand,
        double WeightMultiplier)
    {
        public bool Covers(int year) =>
            year >= StartYear && (EndYear is null || year <= EndYear.Value);
    }
}
