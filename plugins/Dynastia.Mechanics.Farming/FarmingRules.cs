namespace Dynastia.Mechanics.Farming;

public static class FarmingRules
{
    public const decimal PurchasePrice = 10000m;
    public const decimal SalePrice = 8000m;
    public const decimal LivestockPurchasePrice = 2500m;
    public const decimal LivestockSalePrice = 2000m;
    public const decimal WorkerBaseIncomeScale = 2m;
    public const decimal MaximumLivestockIncomeBoost = 0.10m;
    public const decimal MaximumLivestockVolatilityCompression = 0.20m;

    public static int GetActiveWorkerCount(
        int localParcelCount,
        int eligibleWorkers)
    {
        if (localParcelCount <= 0 || eligibleWorkers <= 0)
            return 0;

        return Math.Min(localParcelCount * 2, eligibleWorkers);
    }

    public static decimal GetLivestockCoverage(
        int localParcelCount,
        int localLivestockCount)
    {
        if (localParcelCount <= 0 || localLivestockCount <= 0)
            return 0m;

        return Math.Clamp(
            (decimal)localLivestockCount / localParcelCount,
            0m,
            1m);
    }

    public static decimal GetLivestockIncomeMultiplier(decimal coverage) =>
        1m + MaximumLivestockIncomeBoost * Math.Clamp(coverage, 0m, 1m);

    public static decimal AdjustVolatilityMultiplier(
        decimal rawMultiplier,
        decimal coverage)
    {
        var normalizedCoverage = Math.Clamp(coverage, 0m, 1m);
        return 1m
            + (rawMultiplier - 1m)
            * (1m - MaximumLivestockVolatilityCompression * normalizedCoverage);
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
