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

        const string expectedHeader =
            "StartYear,EndYear,PassiveChanceMultiplier,PassiveMaxLevel,HelpedMaxLevel,FounderMinLevel,FounderMaxLevel,GeneratedAdultMinLevel,GeneratedAdultMaxLevel";

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

        var result = new List<EducationEraRule>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;

            if (fields.Length != 9)
            {
                throw CatalogValidation.FieldCount(
                    DataPath,
                    row,
                    fields.Length,
                    9);
            }

            result.Add(
                new EducationEraRule(
                    CatalogValidation.ParseInt(DataPath, row, "StartYear", fields[0]),
                    string.IsNullOrWhiteSpace(fields[1])
                        ? null
                        : CatalogValidation.ParseInt(DataPath, row, "EndYear", fields[1]),
                    CatalogValidation.ParseDouble(DataPath, row, "PassiveChanceMultiplier", fields[2]),
                    CatalogValidation.ParseInt(DataPath, row, "PassiveMaxLevel", fields[3]),
                    CatalogValidation.ParseInt(DataPath, row, "HelpedMaxLevel", fields[4]),
                    CatalogValidation.ParseInt(DataPath, row, "FounderMinLevel", fields[5]),
                    CatalogValidation.ParseInt(DataPath, row, "FounderMaxLevel", fields[6]),
                    CatalogValidation.ParseInt(DataPath, row, "GeneratedAdultMinLevel", fields[7]),
                    CatalogValidation.ParseInt(DataPath, row, "GeneratedAdultMaxLevel", fields[8])));
        }

        return result;
    }

    private static void Validate(
        IReadOnlyList<EducationEraRule> rules)
    {
        if (rules.Count == 0)
        {
            throw CatalogValidation.Error(
                DataPath,
                "at least one education-era rule",
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

            if (rule.PassiveChanceMultiplier is < 0 or > 1)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a number from 0 through 1",
                    row,
                    field: "PassiveChanceMultiplier",
                    value: rule.PassiveChanceMultiplier);
            }

            ValidateRange(row, rule.PassiveMaxLevel, 0, 5, "PassiveMaxLevel");
            ValidateRange(row, rule.HelpedMaxLevel, 0, 5, "HelpedMaxLevel");
            ValidateLevelPair(row, rule.FounderMinLevel, rule.FounderMaxLevel, "FounderMinLevel", "FounderMaxLevel");
            ValidateLevelPair(row, rule.GeneratedAdultMinLevel, rule.GeneratedAdultMaxLevel, "GeneratedAdultMinLevel", "GeneratedAdultMaxLevel");

            if (rule.HelpedMaxLevel < rule.PassiveMaxLevel)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"a value at least PassiveMaxLevel ({rule.PassiveMaxLevel})",
                    row,
                    field: "HelpedMaxLevel",
                    value: rule.HelpedMaxLevel);
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

    private static void ValidateLevelPair(
        int row,
        int min,
        int max,
        string minField,
        string maxField)
    {
        ValidateRange(row, min, 0, 5, minField);
        ValidateRange(row, max, 0, 5, maxField);

        if (min > max)
        {
            throw CatalogValidation.Error(
                DataPath,
                $"a value at least {minField} ({min})",
                row,
                field: maxField,
                value: max);
        }
    }

    private static void ValidateRange(
        int row,
        int value,
        int min,
        int max,
        string field)
    {
        if (value < min || value > max)
        {
            throw CatalogValidation.Error(
                DataPath,
                $"an integer from {min} through {max}",
                row,
                field: field,
                value: value);
        }
    }
}
