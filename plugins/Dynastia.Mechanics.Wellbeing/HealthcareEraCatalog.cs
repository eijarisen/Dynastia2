using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Wellbeing;

public sealed class HealthcareEraCatalog
{
    private const string DataPath =
        "Health/healthcare_eras.csv";

    private readonly IReadOnlyList<HealthcareEraRule> _rules;

    private HealthcareEraCatalog(IReadOnlyList<HealthcareEraRule> rules)
    {
        _rules = rules;
    }

    public static HealthcareEraCatalog Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var rules = Parse(data.ReadText(DataPath));
        Validate(rules);
        return new HealthcareEraCatalog(rules.OrderBy(rule => rule.StartYear).ToList());
    }

    public double GetHealAmount(int year)
    {
        var effectiveYear = Math.Max(year, GameCalendarConfiguration.GameStartYear);
        return _rules.First(rule => rule.Covers(effectiveYear)).HealAmount;
    }

    private static IReadOnlyList<HealthcareEraRule> Parse(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string expectedHeader = "StartYear,EndYear,HealAmount";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(expectedHeader, StringComparison.Ordinal))
            throw new InvalidDataException($"{DataPath} has an unexpected header or is empty.");

        var result = new List<HealthcareEraRule>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 3)
                throw new InvalidDataException($"Invalid {DataPath} row {index + 1}: expected 3 fields.");

            result.Add(new HealthcareEraRule(
                ParseInt(fields[0], index),
                string.IsNullOrWhiteSpace(fields[1]) ? null : ParseInt(fields[1], index),
                ParseDouble(fields[2], index)));
        }
        return result;
    }

    private static void Validate(IReadOnlyList<HealthcareEraRule> rules)
    {
        if (rules.Count == 0)
            throw new InvalidDataException($"{DataPath} contains no rules.");

        var ordered = rules.OrderBy(rule => rule.StartYear).ToList();
        if (ordered[0].StartYear != GameCalendarConfiguration.GameStartYear)
            throw new InvalidDataException($"{DataPath} must begin in {GameCalendarConfiguration.GameStartYear}.");

        for (var index = 0; index < ordered.Count; index++)
        {
            var rule = ordered[index];
            if (rule.HealAmount <= 0)
                throw new InvalidDataException($"{DataPath}: HealAmount must be positive.");
            if (rule.EndYear is int endYear && endYear < rule.StartYear)
                throw new InvalidDataException($"{DataPath}: end year precedes start year.");

            if (index < ordered.Count - 1)
            {
                if (rule.EndYear is not int currentEnd || ordered[index + 1].StartYear != currentEnd + 1)
                    throw new InvalidDataException($"{DataPath} contains a gap or overlap in era coverage.");
            }
            else if (rule.EndYear is not null)
            {
                throw new InvalidDataException($"{DataPath} must end with an open-ended rule.");
            }
        }
    }

    private static int ParseInt(string value, int rowIndex)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"Invalid year in {DataPath} row {rowIndex + 2}.");
        return parsed;
    }

    private static double ParseDouble(string value, int rowIndex)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"Invalid HealAmount in {DataPath} row {rowIndex + 2}.");
        return parsed;
    }
}

public sealed record HealthcareEraRule(
    int StartYear,
    int? EndYear,
    double HealAmount)
{
    public bool Covers(int year) =>
        year >= StartYear && (EndYear is null || year <= EndYear.Value);
}
