namespace Dynastia.Mechanics.Farming;

public static class FarmingRules
{
    public const decimal PurchasePrice = 10000m;
    public const decimal SalePrice = 8000m;

    public static IReadOnlyList<decimal> GetStaffingFactors(
        int localParcelCount,
        int eligibleWorkers)
    {
        if (localParcelCount <= 0 || eligibleWorkers <= 0)
            return [];

        var factors = new List<decimal>();
        var remainingWorkers = eligibleWorkers;

        for (var parcel = 0;
            parcel < localParcelCount && remainingWorkers > 0;
            parcel++)
        {
            if (remainingWorkers >= 2)
            {
                factors.Add(1.0m);
                remainingWorkers -= 2;
            }
            else
            {
                factors.Add(0.5m);
                remainingWorkers = 0;
            }
        }

        return factors;
    }

    public static decimal InterpolateEraMultiplier(
        int year,
        IReadOnlyList<(int Year, decimal Multiplier)> anchors)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        if (anchors.Count == 0)
            throw new ArgumentException("At least one farming-era anchor is required.", nameof(anchors));

        var ordered = anchors.OrderBy(anchor => anchor.Year).ToList();

        if (year <= ordered[0].Year)
            return ordered[0].Multiplier;

        if (year >= ordered[^1].Year)
            return ordered[^1].Multiplier;

        for (var index = 0; index < ordered.Count - 1; index++)
        {
            var lower = ordered[index];
            var upper = ordered[index + 1];

            if (year < lower.Year || year > upper.Year)
                continue;

            var span = upper.Year - lower.Year;
            if (span <= 0)
                return upper.Multiplier;

            var position = (decimal)(year - lower.Year) / span;
            return lower.Multiplier
                + (upper.Multiplier - lower.Multiplier) * position;
        }

        return ordered[^1].Multiplier;
    }
}
