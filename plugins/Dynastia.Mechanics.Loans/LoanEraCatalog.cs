using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

public sealed class LoanEraCatalog
{
    private const string DataPath =
        "Loans/loan_eras.csv";

    private readonly IReadOnlyList<LoanEraRule> _rules;

    private LoanEraCatalog(
        IReadOnlyList<LoanEraRule> rules)
    {
        _rules = rules;
    }

    public static LoanEraCatalog Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var rules = Parse(data.ReadText(DataPath));
        Validate(rules);

        return new LoanEraCatalog(
            rules.OrderBy(rule => rule.StartYear).ToList());
    }

    public LoanEraRule GetRule(int year)
    {
        var effectiveYear = Math.Max(
            year,
            GameCalendarConfiguration.GameStartYear);

        return _rules.First(rule => rule.Covers(effectiveYear));
    }

    private static IReadOnlyList<LoanEraRule> Parse(
        string text)
    {
        var lines = text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);

        const string expectedHeader =
            "StartYear,EndYear,ExternalCreditorLabel,TakeLoanEventPhrase";

        if (lines.Length < 2
            || !lines[0].Equals(
                expectedHeader,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{DataPath} has an unexpected header or is empty.");
        }

        var result = new List<LoanEraRule>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');

            if (fields.Length != 4)
            {
                throw new InvalidDataException(
                    $"Invalid {DataPath} row {index + 1}: expected 4 fields.");
            }

            result.Add(
                new LoanEraRule(
                    ParseInt(fields[0], index),
                    string.IsNullOrWhiteSpace(fields[1])
                        ? null
                        : ParseInt(fields[1], index),
                    fields[2].Trim(),
                    fields[3].Trim()));
        }

        return result;
    }

    private static void Validate(
        IReadOnlyList<LoanEraRule> rules)
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

            if (string.IsNullOrWhiteSpace(rule.ExternalCreditorLabel)
                || string.IsNullOrWhiteSpace(rule.TakeLoanEventPhrase))
            {
                throw new InvalidDataException(
                    $"{DataPath} contains an incomplete rule.");
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
}

public sealed record LoanEraRule(
    int StartYear,
    int? EndYear,
    string ExternalCreditorLabel,
    string TakeLoanEventPhrase)
{
    public bool Covers(int year) =>
        year >= StartYear
        && (EndYear is null || year <= EndYear.Value);
}
