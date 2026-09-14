using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Education;

public sealed class EducationEraCatalog
{
    private const string DataPath =
        "Education/education_eras.csv";

    private readonly IReadOnlyList<EducationEraRule> _rules;

    private EducationEraCatalog(
        IReadOnlyList<EducationEraRule> rules)
    {
        _rules = rules;
    }

    public static EducationEraCatalog Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var rules = Parse(data.ReadText(DataPath));
        Validate(rules);

        return new EducationEraCatalog(rules);
    }

    public EducationEraRule GetRule(int year)
    {
        var effectiveYear = Math.Max(
            year,
            GameCalendarConfiguration.GameStartYear);

        return _rules.First(rule => rule.Covers(effectiveYear));
    }

    private static IReadOnlyList<EducationEraRule> Parse(
        string text)
    {
        var lines = text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
        {
            throw new InvalidDataException(
                $"{DataPath} is empty.");
        }

        const string expectedHeader =
            "StartYear,EndYear,PassiveChanceMultiplier,PassiveMaxLevel,HelpedMaxLevel,FounderMinLevel,FounderMaxLevel,GeneratedAdultMinLevel,GeneratedAdultMaxLevel";

        if (!lines[0].Equals(
                expectedHeader,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{DataPath} has an unexpected header.");
        }

        var result = new List<EducationEraRule>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');

            if (fields.Length != 9)
            {
                throw new InvalidDataException(
                    $"Invalid {DataPath} row {index + 1}: expected 9 fields.");
            }

            result.Add(
                new EducationEraRule(
                    ParseInt(fields[0], index),
                    string.IsNullOrWhiteSpace(fields[1])
                        ? null
                        : ParseInt(fields[1], index),
                    ParseDouble(fields[2], index),
                    ParseInt(fields[3], index),
                    ParseInt(fields[4], index),
                    ParseInt(fields[5], index),
                    ParseInt(fields[6], index),
                    ParseInt(fields[7], index),
                    ParseInt(fields[8], index)));
        }

        return result;
    }

    private static void Validate(
        IReadOnlyList<EducationEraRule> rules)
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

            if (rule.PassiveChanceMultiplier is < 0 or > 1)
            {
                throw new InvalidDataException(
                    $"{DataPath}: passive multiplier must be between 0 and 1.");
            }

            ValidateRange(
                rule.PassiveMaxLevel,
                0,
                5,
                "PassiveMaxLevel");
            ValidateRange(
                rule.HelpedMaxLevel,
                0,
                5,
                "HelpedMaxLevel");
            ValidateLevelPair(
                rule.FounderMinLevel,
                rule.FounderMaxLevel,
                "founder");
            ValidateLevelPair(
                rule.GeneratedAdultMinLevel,
                rule.GeneratedAdultMaxLevel,
                "generated adult");

            if (rule.HelpedMaxLevel < rule.PassiveMaxLevel)
            {
                throw new InvalidDataException(
                    $"{DataPath}: helped maximum may not be below passive maximum.");
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

    private static void ValidateLevelPair(
        int min,
        int max,
        string name)
    {
        ValidateRange(min, 0, 5, $"{name} minimum");
        ValidateRange(max, 0, 5, $"{name} maximum");

        if (min > max)
        {
            throw new InvalidDataException(
                $"{DataPath}: {name} minimum exceeds maximum.");
        }
    }

    private static void ValidateRange(
        int value,
        int min,
        int max,
        string name)
    {
        if (value < min || value > max)
        {
            throw new InvalidDataException(
                $"{DataPath}: {name} must be between {min} and {max}.");
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
                $"Invalid number in {DataPath} row {rowIndex + 2}.");
        }

        return parsed;
    }
}
