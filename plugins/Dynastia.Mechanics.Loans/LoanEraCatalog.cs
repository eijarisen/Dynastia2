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
            || !lines[0].TrimStart('\uFEFF').Equals(
                expectedHeader,
                StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                DataPath,
                lines.Length == 0
                    ? null
                    : lines[0].TrimStart('\uFEFF'),
                expectedHeader);
        }

        var result = new List<LoanEraRule>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;

            if (fields.Length != 4)
            {
                throw CatalogValidation.FieldCount(
                    DataPath,
                    row,
                    fields.Length,
                    4);
            }

            result.Add(
                new LoanEraRule(
                    CatalogValidation.ParseInt(DataPath, row, "StartYear", fields[0]),
                    string.IsNullOrWhiteSpace(fields[1])
                        ? null
                        : CatalogValidation.ParseInt(DataPath, row, "EndYear", fields[1]),
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
            throw CatalogValidation.Error(
                DataPath,
                "at least one loan-era rule",
                field: "Rows",
                value: 0);
        }

        var ordered = rules
            .Select((rule, index) => (Rule: rule, Row: index + 2))
            .OrderBy(entry => entry.Rule.StartYear)
            .ToList();

        if (ordered[0].Rule.StartYear
            != GameCalendarConfiguration.GameStartYear)
        {
            throw CatalogValidation.Error(
                DataPath,
                $"{GameCalendarConfiguration.GameStartYear} for the first rule",
                ordered[0].Row,
                field: "StartYear",
                value: ordered[0].Rule.StartYear);
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            var (rule, row) = ordered[index];

            if (string.IsNullOrWhiteSpace(rule.ExternalCreditorLabel))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a non-empty creditor label",
                    row,
                    field: "ExternalCreditorLabel",
                    value: rule.ExternalCreditorLabel);
            }

            if (string.IsNullOrWhiteSpace(rule.TakeLoanEventPhrase))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a non-empty event phrase",
                    row,
                    field: "TakeLoanEventPhrase",
                    value: rule.TakeLoanEventPhrase);
            }

            if (rule.EndYear is int endYear
                && endYear < rule.StartYear)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"a year at or after StartYear ({rule.StartYear})",
                    row,
                    field: "EndYear",
                    value: endYear);
            }

            if (index < ordered.Count - 1)
            {
                var next = ordered[index + 1];
                if (rule.EndYear is not int currentEnd
                    || next.Rule.StartYear != currentEnd + 1)
                {
                    throw CatalogValidation.Error(
                        DataPath,
                        rule.EndYear is int closedEnd
                            ? $"{closedEnd + 1} so era coverage is contiguous"
                            : "an EndYear on every non-final era",
                        next.Row,
                        field: "StartYear",
                        value: next.Rule.StartYear);
                }
            }
            else if (rule.EndYear is not null)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "an empty EndYear for the final open-ended rule",
                    row,
                    field: "EndYear",
                    value: rule.EndYear.Value);
            }
        }
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
