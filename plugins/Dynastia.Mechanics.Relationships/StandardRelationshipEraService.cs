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

        var result = new List<RelationshipEraRule>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;

            if (fields.Length != 5)
            {
                throw CatalogValidation.FieldCount(
                    DataPath,
                    row,
                    fields.Length,
                    5);
            }

            result.Add(
                new RelationshipEraRule(
                    CatalogValidation.ParseInt(DataPath, row, "StartYear", fields[0]),
                    string.IsNullOrWhiteSpace(fields[1])
                        ? null
                        : CatalogValidation.ParseInt(DataPath, row, "EndYear", fields[1]),
                    CatalogValidation.ParseDouble(DataPath, row, "MarriageChanceMultiplier", fields[2]),
                    CatalogValidation.ParseDouble(DataPath, row, "ArrangedMarriageMultiplier", fields[3]),
                    CatalogValidation.ParseDouble(DataPath, row, "AutomaticDivorceMultiplier", fields[4])));
        }

        return result;
    }

    private static void Validate(
        IReadOnlyList<RelationshipEraRule> rules)
    {
        if (rules.Count == 0)
        {
            throw CatalogValidation.Error(
                DataPath,
                "at least one relationship-era rule",
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

            ValidatePositiveMultiplier(
                row,
                "MarriageChanceMultiplier",
                rule.MarriageChanceMultiplier);
            ValidatePositiveMultiplier(
                row,
                "ArrangedMarriageMultiplier",
                rule.ArrangedMarriageMultiplier);
            ValidatePositiveMultiplier(
                row,
                "AutomaticDivorceMultiplier",
                rule.AutomaticDivorceMultiplier);

            if (rule.AutomaticDivorceMultiplier > 1.0)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a number greater than 0 and no greater than 1",
                    row,
                    field: "AutomaticDivorceMultiplier",
                    value: rule.AutomaticDivorceMultiplier);
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

    private static void ValidatePositiveMultiplier(
        int row,
        string field,
        double value)
    {
        if (value <= 0)
        {
            throw CatalogValidation.Error(
                DataPath,
                "a number greater than 0",
                row,
                field: field,
                value: value);
        }
    }
}
