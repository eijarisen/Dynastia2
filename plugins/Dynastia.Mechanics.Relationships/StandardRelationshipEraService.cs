using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class StandardRelationshipEraService :
    IRelationshipEraService
{
    private const string DataPath =
        "Relationships/relationship_eras.csv";

    private readonly IReadOnlyList<RelationshipEraRule> _rules;

    private StandardRelationshipEraService(
        IReadOnlyList<RelationshipEraRule> rules)
    {
        _rules = rules;
    }

    public static StandardRelationshipEraService Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var rules = Parse(data.ReadText(DataPath));
        Validate(rules);

        return new StandardRelationshipEraService(
            rules.OrderBy(rule => rule.StartYear).ToList());
    }

    public RelationshipEraRule GetRule(int year)
    {
        var effectiveYear = Math.Max(
            year,
            GameCalendarConfiguration.GameStartYear);

        return _rules.First(rule => rule.Covers(effectiveYear));
    }

    private static IReadOnlyList<RelationshipEraRule> Parse(
        string text)
    {
        var lines = text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);

        const string expectedHeader =
            "StartYear,EndYear,MarriageChanceMultiplier,ArrangedMarriageMultiplier,AutomaticDivorceMultiplier";

        if (lines.Length < 2
            || !lines[0].Equals(
                expectedHeader,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{DataPath} has an unexpected header or is empty.");
        }

        var result = new List<RelationshipEraRule>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');

            if (fields.Length != 5)
            {
                throw new InvalidDataException(
                    $"Invalid {DataPath} row {index + 1}: expected 5 fields.");
            }

            result.Add(
                new RelationshipEraRule(
                    ParseInt(fields[0], index),
                    string.IsNullOrWhiteSpace(fields[1])
                        ? null
                        : ParseInt(fields[1], index),
                    ParseDouble(fields[2], index),
                    ParseDouble(fields[3], index),
                    ParseDouble(fields[4], index)));
        }

        return result;
    }

    private static void Validate(
        IReadOnlyList<RelationshipEraRule> rules)
    {
        if (rules.Count == 0)
        {
            throw new InvalidDataException(
                $"{DataPath} contains no rules.");
        }

        var ordered = rules.OrderBy(rule => rule.StartYear).ToList();

        if (ordered[0].StartYear
            != GameCalendarConfiguration.GameStartYear)
        {
            throw new InvalidDataException(
                $"{DataPath} must begin in " +
                $"{GameCalendarConfiguration.GameStartYear}.");
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            var rule = ordered[index];

            if (rule.EndYear is int endYear
                && endYear < rule.StartYear)
            {
                throw new InvalidDataException(
                    $"{DataPath}: end year precedes start year.");
            }

            if (rule.MarriageChanceMultiplier <= 0
                || rule.ArrangedMarriageMultiplier <= 0
                || rule.AutomaticDivorceMultiplier <= 0)
            {
                throw new InvalidDataException(
                    $"{DataPath}: all multipliers must be positive.");
            }

            if (rule.AutomaticDivorceMultiplier > 1.0)
            {
                throw new InvalidDataException(
                    $"{DataPath}: automatic-divorce multiplier may not exceed 1.0.");
            }

            if (index < ordered.Count - 1)
            {
                if (rule.EndYear is not int currentEnd
                    || ordered[index + 1].StartYear != currentEnd + 1)
                {
                    throw new InvalidDataException(
                        $"{DataPath} contains a gap or overlap in era coverage.");
                }
            }
            else if (rule.EndYear is not null)
            {
                throw new InvalidDataException(
                    $"{DataPath} must end with an open-ended rule.");
            }
        }
    }

    private static int ParseInt(
        string value,
        int rowIndex)
    {
        if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            throw new InvalidDataException(
                $"Invalid year in {DataPath} row {rowIndex + 2}.");
        }

        return parsed;
    }

    private static double ParseDouble(
        string value,
        int rowIndex)
    {
        if (!double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            throw new InvalidDataException(
                $"Invalid multiplier in {DataPath} row {rowIndex + 2}.");
        }

        return parsed;
    }
}
