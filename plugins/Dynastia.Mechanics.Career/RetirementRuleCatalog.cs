using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed record RetirementRule(
    int StartYear,
    int? EndYear,
    int MaleRetirementAge,
    int FemaleRetirementAge,
    decimal PensionRate)
{
    public int GetRetirementAge(
        Sex sex) =>
        sex == Sex.Male
            ? MaleRetirementAge
            : FemaleRetirementAge;
}

public sealed class RetirementRuleCatalog
{
    private const string DataPath =
        "Career/retirement_rules.csv";

    private readonly IReadOnlyList<RetirementRule>
        _rules;

    private RetirementRuleCatalog(
        IReadOnlyList<RetirementRule> rules)
    {
        _rules = rules;
    }

    public static RetirementRuleCatalog Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var rules =
            Parse(
                data.ReadText(DataPath));

        Validate(rules);

        return new RetirementRuleCatalog(rules);
    }

    public RetirementRule GetRule(
        int gameYear)
    {
        var effectiveYear =
            Math.Min(
                Math.Max(
                    gameYear,
                    GameCalendarConfiguration.GameStartYear),
                GameCalendarConfiguration.TechnologyFreezeYear);

        return _rules.First(rule =>
            effectiveYear >= rule.StartYear
            && (rule.EndYear is null
                || effectiveYear <= rule.EndYear.Value));
    }

    private static IReadOnlyList<RetirementRule>
        Parse(
            string text)
    {
        var lines =
            text.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
        {
            throw new InvalidDataException(
                $"{DataPath} is empty.");
        }

        var expectedHeader =
            "StartYear,EndYear,MaleRetirementAge,FemaleRetirementAge,PensionRate";

        if (!lines[0].Equals(
                expectedHeader,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{DataPath} has an unexpected header.");
        }

        var result =
            new List<RetirementRule>();

        for (var index = 1;
            index < lines.Length;
            index++)
        {
            var fields =
                lines[index].Split(',');

            if (fields.Length != 5)
            {
                throw new InvalidDataException(
                    $"Invalid {DataPath} row {index + 1}: " +
                    "expected 5 fields.");
            }

            result.Add(
                new RetirementRule(
                    ParseInt(fields[0], index),
                    string.IsNullOrWhiteSpace(fields[1])
                        ? null
                        : ParseInt(fields[1], index),
                    ParseInt(fields[2], index),
                    ParseInt(fields[3], index),
                    ParseDecimal(fields[4], index)));
        }

        return result;
    }

    private static void Validate(
        IReadOnlyList<RetirementRule> rules)
    {
        if (rules.Count == 0)
        {
            throw new InvalidDataException(
                $"{DataPath} contains no rules.");
        }

        var ordered =
            rules.OrderBy(rule => rule.StartYear)
                .ToList();

        if (ordered[0].StartYear
            != GameCalendarConfiguration.GameStartYear)
        {
            throw new InvalidDataException(
                $"{DataPath} must begin in " +
                $"{GameCalendarConfiguration.GameStartYear}.");
        }

        for (var index = 0;
            index < ordered.Count;
            index++)
        {
            var rule = ordered[index];

            if (rule.MaleRetirementAge <= 0
                || rule.FemaleRetirementAge <= 0)
            {
                throw new InvalidDataException(
                    $"{DataPath}: retirement ages must be positive.");
            }

            if (rule.PensionRate < 0
                || rule.PensionRate > 1)
            {
                throw new InvalidDataException(
                    $"{DataPath}: pension rate must be between 0 and 1.");
            }

            if (rule.EndYear is int endYear
                && endYear < rule.StartYear)
            {
                throw new InvalidDataException(
                    $"{DataPath}: end year precedes start year.");
            }

            if (index < ordered.Count - 1)
            {
                if (rule.EndYear is not int currentEnd
                    || ordered[index + 1].StartYear
                        != currentEnd + 1)
                {
                    throw new InvalidDataException(
                        $"{DataPath} contains a gap or overlap " +
                        "in retirement coverage.");
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
                $"Invalid integer in {DataPath} row {rowIndex + 2}.");
        }

        return parsed;
    }

    private static decimal ParseDecimal(
        string value,
        int rowIndex)
    {
        if (!decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            throw new InvalidDataException(
                $"Invalid decimal in {DataPath} row {rowIndex + 2}.");
        }

        return parsed;
    }
}
