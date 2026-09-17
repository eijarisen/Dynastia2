using System.Globalization;
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
        var lines = data.ReadText(Path).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "ConditionId,StartYear,EndYear,MotherAgeBand,WeightMultiplier";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{Path} has an unexpected header or is empty.");

        var rows = new List<Rule>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 5)
                throw new InvalidDataException($"{Path} row {row}: expected 5 fields.");

            var id = fields[0].Trim();
            if (!known.Contains(id))
                throw new InvalidDataException($"{Path} row {row} ConditionId: unknown condition '{id}'.");

            var start = ParseInt(fields[1], row, "StartYear");
            var end = string.IsNullOrWhiteSpace(fields[2]) ? null : ParseInt(fields[2], row, "EndYear");
            var ageBand = fields[3].Trim();
            var multiplier = ParseDouble(fields[4], row, "WeightMultiplier");

            if (start < GameCalendarConfiguration.GameStartYear)
                throw new InvalidDataException($"{Path} row {row} StartYear: may not precede {GameCalendarConfiguration.GameStartYear}.");
            if (end is int endYear && endYear < start)
                throw new InvalidDataException($"{Path} row {row} EndYear: may not precede StartYear.");
            if (ageBand is not ("YoungAdult" or "Adult"))
                throw new InvalidDataException($"{Path} row {row} MotherAgeBand: invalid value '{ageBand}'.");
            if (multiplier <= 0)
                throw new InvalidDataException($"{Path} row {row} WeightMultiplier: must be greater than 0.");

            rows.Add(new Rule(id, start, end, ageBand, multiplier));
        }

        foreach (var group in rows.GroupBy(
                     rule => (rule.ConditionId.ToUpperInvariant(), rule.MotherAgeBand.ToUpperInvariant())))
        {
            var ordered = group.OrderBy(rule => rule.StartYear).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                if (previous.EndYear is null || previous.EndYear.Value >= ordered[index].StartYear)
                    throw new InvalidDataException($"{Path}: overlapping rows for '{ordered[index].ConditionId}'/{ordered[index].MotherAgeBand}.");
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

    private sealed record Rule(
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
