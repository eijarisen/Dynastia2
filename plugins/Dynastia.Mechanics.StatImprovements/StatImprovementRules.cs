using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.StatImprovements;

internal static class StatImprovementRules
{
    private const string DataPath =
        "LocalSociety/medical_stat_improvement_rules.csv";

    public static IReadOnlyList<PaidStatImprovementDefinition> Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var lines = data.ReadText(DataPath)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length < 2)
            throw new InvalidDataException($"{DataPath}: expected a header and at least one rule.");

        var headers = lines[0]
            .TrimStart('\uFEFF')
            .Split(',')
            .Select(header => header.Trim())
            .ToArray();

        var rows = new List<PaidStatImprovementDefinition>();
        for (var index = 1; index < lines.Length; index++)
        {
            var values = lines[index]
                .Split(',')
                .Select(value => value.Trim())
                .ToArray();

            if (values.Length != headers.Length)
                throw new InvalidDataException($"{DataPath}: row {index + 1} has an unexpected column count.");

            var row = headers
                .Select((header, column) => new { header, value = values[column] })
                .ToDictionary(item => item.header, item => item.value, StringComparer.OrdinalIgnoreCase);

            var actionId = Required(row, "ActionId", index + 1);
            var statId = Required(row, "StatId", index + 1);
            var displayName = Required(row, "DisplayName", index + 1);

            if (!int.TryParse(
                    Required(row, "MinimumMedicalTier", index + 1),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var minimumMedicalTier)
                || minimumMedicalTier is < 1 or > 5)
            {
                throw new InvalidDataException(
                    $"{DataPath}: row {index + 1} has an invalid MinimumMedicalTier.");
            }

            if (!decimal.TryParse(
                    Required(row, "BaseCost", index + 1),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var baseCost)
                || baseCost <= 0)
            {
                throw new InvalidDataException(
                    $"{DataPath}: row {index + 1} has an invalid BaseCost.");
            }

            rows.Add(new PaidStatImprovementDefinition(
                actionId,
                statId,
                displayName,
                minimumMedicalTier,
                baseCost));
        }

        var duplicate = rows
            .GroupBy(row => row.ActionId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"{DataPath}: duplicate action '{duplicate.Key}'.");

        return rows;
    }

    public static decimal CalculateCost(
        decimal baseCost,
        decimal treatmentCostMultiplier) =>
        Math.Round(
            baseCost * treatmentCostMultiplier,
            0,
            MidpointRounding.AwayFromZero);

    private static string Required(
        IReadOnlyDictionary<string, string> row,
        string field,
        int line)
    {
        if (!row.TryGetValue(field, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"{DataPath}: row {line} is missing '{field}'.");
        }

        return value;
    }
}
