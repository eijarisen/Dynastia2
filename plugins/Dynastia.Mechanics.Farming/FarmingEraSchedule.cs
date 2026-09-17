using Dynastia.Contracts;

namespace Dynastia.Mechanics.Farming;

internal sealed class FarmingEraSchedule
{
    private const string DataPath =
        "Farming/farming_era_multipliers.csv";

    private readonly IReadOnlyList<(int Year, decimal Multiplier)> _anchors;

    private FarmingEraSchedule(
        IReadOnlyList<(int Year, decimal Multiplier)> anchors)
    {
        _anchors = anchors;
    }

    public static FarmingEraSchedule Load(
        IGameDataService data)
    {
        var lines = data.ReadText(DataPath)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        const string expectedHeader = "Year,Multiplier";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(expectedHeader, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                DataPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                expectedHeader);
        }

        var anchors = new List<(int Year, decimal Multiplier)>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 2)
                throw CatalogValidation.FieldCount(DataPath, row, fields.Length, 2);

            var year = CatalogValidation.ParseInt(DataPath, row, "Year", fields[0]);
            var multiplier = CatalogValidation.ParseDecimal(DataPath, row, "Multiplier", fields[1]);
            if (multiplier < 0)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a number greater than or equal to 0",
                    row,
                    field: "Multiplier",
                    value: multiplier);
            }

            anchors.Add((year, multiplier));
        }

        if (anchors.Count < 2)
        {
            throw CatalogValidation.Error(
                DataPath,
                "at least two anchor rows",
                field: "Rows",
                value: anchors.Count);
        }

        return new FarmingEraSchedule(anchors);
    }

    public decimal GetMultiplier(int year) =>
        FarmingRules.InterpolateEraMultiplier(year, _anchors);
}
