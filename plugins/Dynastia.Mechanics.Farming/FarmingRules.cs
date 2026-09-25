namespace Dynastia.Mechanics.Farming;

public static class FarmingRules
{
    public const decimal PurchasePrice = 10000m;
    public const decimal MinimumPurchasePrice = 9000m;
    public const decimal MaximumPurchasePrice = 11000m;
    public const decimal PurchasePriceStep = 100m;
    public const decimal SalePrice = 8000m;
    public const decimal LivestockPurchasePrice = 2500m;
    public const decimal LivestockSalePrice = 2000m;
    public const decimal WorkerBaseIncomeScale = 2m;
    public const decimal MaximumLivestockIncomeBoost = 0.10m;
    public const decimal MaximumLivestockVolatilityCompression = 0.20m;

    public static decimal GetMarketPurchasePrice(double unitRoll)
    {
        var normalized = Math.Clamp(unitRoll, 0d, 1d);
        var stepCount =
            (int)((MaximumPurchasePrice - MinimumPurchasePrice)
                / PurchasePriceStep)
            + 1;
        var index = normalized >= 1d
            ? stepCount - 1
            : Math.Min(stepCount - 1, (int)Math.Floor(normalized * stepCount));

        return MinimumPurchasePrice + index * PurchasePriceStep;
    }

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

    public static decimal GetRawWeatherYieldMultiplier(
        double winterTemperature,
        double springTemperature,
        double summerPrecipitation,
        double autumnPrecipitation)
    {
        var winterFavourability = -(decimal)Math.Clamp(winterTemperature, -1.0, 1.0);
        var springFavourability = (decimal)Math.Clamp(springTemperature, -1.0, 1.0);
        var summerFavourability = (decimal)Math.Clamp(summerPrecipitation, -1.0, 1.0);
        var autumnFavourability = -(decimal)Math.Clamp(autumnPrecipitation, -1.0, 1.0);

        return Math.Clamp(
            1.00m
            + 0.25m * winterFavourability
            + 0.25m * springFavourability
            + 0.25m * summerFavourability
            + 0.25m * autumnFavourability,
            0.00m,
            2.00m);
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
