using System.Globalization;
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

        if (lines.Length < 2
            || !lines[0].Equals("Year,Multiplier", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{DataPath} must use the header Year,Multiplier.");
        }

        var anchors = new List<(int Year, decimal Multiplier)>();

        foreach (var line in lines.Skip(1))
        {
            var fields = line.Split(',');
            if (fields.Length != 2
                || !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
                || !decimal.TryParse(fields[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var multiplier)
                || multiplier < 0)
            {
                throw new InvalidDataException(
                    $"Invalid farming-era row: {line}");
            }

            anchors.Add((year, multiplier));
        }

        if (anchors.Count < 2)
            throw new InvalidDataException($"{DataPath} must contain at least two anchors.");

        return new FarmingEraSchedule(anchors);
    }

    public decimal GetMultiplier(int year) =>
        FarmingRules.InterpolateEraMultiplier(year, _anchors);
}
