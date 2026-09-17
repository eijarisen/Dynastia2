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

        const string expectedHeader =
            "StartYear,EndYear,MaleRetirementAge,FemaleRetirementAge,PensionRate";

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

        var result =
            new List<RetirementRule>();

        for (var index = 1;
            index < lines.Length;
            index++)
        {
            var fields =
                lines[index].Split(',');
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
                new RetirementRule(
                    CatalogValidation.ParseInt(
                        DataPath,
                        row,
                        "StartYear",
                        fields[0]),
                    string.IsNullOrWhiteSpace(fields[1])
                        ? null
                        : CatalogValidation.ParseInt(
                            DataPath,
                            row,
                            "EndYear",
                            fields[1]),
                    CatalogValidation.ParseInt(
                        DataPath,
                        row,
                        "MaleRetirementAge",
                        fields[2]),
                    CatalogValidation.ParseInt(
                        DataPath,
                        row,
                        "FemaleRetirementAge",
                        fields[3]),
                    CatalogValidation.ParseDecimal(
                        DataPath,
                        row,
                        "PensionRate",
                        fields[4])));
        }

        return result;
    }

    private static void Validate(
        IReadOnlyList<RetirementRule> rules)
    {
        if (rules.Count == 0)
        {
            throw CatalogValidation.Error(
                DataPath,
                "at least one retirement rule",
                field: "Rows",
                value: 0);
        }

        var ordered =
            rules.Select(
                    (rule, index) =>
                        (Rule: rule, Row: index + 2))
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

        for (var index = 0;
            index < ordered.Count;
            index++)
        {
            var (rule, row) = ordered[index];

            if (rule.MaleRetirementAge <= 0)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "an age greater than 0",
                    row,
                    field: "MaleRetirementAge",
                    value: rule.MaleRetirementAge);
            }

            if (rule.FemaleRetirementAge <= 0)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "an age greater than 0",
                    row,
                    field: "FemaleRetirementAge",
                    value: rule.FemaleRetirementAge);
            }

            if (rule.PensionRate < 0
                || rule.PensionRate > 1)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a number from 0 through 1",
                    row,
                    field: "PensionRate",
                    value: rule.PensionRate);
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
                            : "an open-ended final rule only; non-final rules require EndYear",
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
