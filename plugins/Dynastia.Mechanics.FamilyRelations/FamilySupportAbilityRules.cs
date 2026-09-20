namespace Dynastia.Mechanics.FamilyRelations;

public static class FamilySupportAbilityRules
{
    private const decimal MinimumCashReserve = 2000m;

    public static bool HasStrongCareerConnectionRelation(
        double familiarity,
        double sympathy)
    {
        familiarity = Math.Clamp(familiarity, 0, 100);
        sympathy = Math.Clamp(sympathy, 0, 100);

        // The existing relation labels are two-dimensional: Warm comes from
        // Sympathy while Close comes from Familiarity. Either is sufficient
        // for a strong family connection.
        return sympathy >= 60
            || familiarity >= 75;
    }

    public static double GetMoneyRequestAbilityFactor(
        decimal donorWealth,
        decimal requesterWealth,
        decimal projectedAnnualExpenses,
        decimal requestedAmount,
        int houseCount,
        int farmlandCount)
    {
        if (requestedAmount <= 0m)
            return 0;

        var reserve = GetCashReserve(projectedAnnualExpenses);
        var postGiftWealth = donorWealth - requestedAmount;
        if (postGiftWealth < reserve)
            return 0;

        var surplusRatio = (double)(postGiftWealth - reserve)
            / (double)Math.Max(reserve, 1000m);
        var relativeRatio = (double)Math.Max(donorWealth, 0m)
            / (double)Math.Max(requesterWealth + 1000m, 1000m);
        var spareHouses = Math.Max(0, houseCount - 1);
        var spareFarmland = Math.Max(0, farmlandCount - 1);

        var factor = 0.55
            + Math.Min(0.35, Math.Max(0, surplusRatio) * 0.12)
            + Math.Min(0.20, Math.Max(0, relativeRatio - 1.0) * 0.10)
            + Math.Min(0.15, spareHouses * 0.08 + spareFarmland * 0.05);

        return Math.Clamp(factor, 0.35, 1.25);
    }

    public static double GetHouseRequestAbilityFactor(
        decimal donorWealth,
        decimal requesterWealth,
        decimal projectedAnnualExpenses,
        int houseCount,
        int farmlandCount)
    {
        // One house is the household's essential residence and is never
        // considered spare family support.
        if (houseCount <= 1)
            return 0;

        if (!HasFinancialBreathingRoom(donorWealth, projectedAnnualExpenses))
            return 0;

        return GetAssetRequestAbilityFactor(
            donorWealth,
            requesterWealth,
            projectedAnnualExpenses,
            sparePrimaryAssets: houseCount - 1,
            spareSecondaryAssets: Math.Max(0, farmlandCount - 1));
    }

    public static double GetFarmlandRequestAbilityFactor(
        decimal donorWealth,
        decimal requesterWealth,
        decimal projectedAnnualExpenses,
        int farmlandCount,
        int houseCount)
    {
        // Keep the final parcel as an essential productive asset. Requests
        // become viable only when the donor truly has land to spare.
        if (farmlandCount <= 1)
            return 0;

        if (!HasFinancialBreathingRoom(donorWealth, projectedAnnualExpenses))
            return 0;

        return GetAssetRequestAbilityFactor(
            donorWealth,
            requesterWealth,
            projectedAnnualExpenses,
            sparePrimaryAssets: farmlandCount - 1,
            spareSecondaryAssets: Math.Max(0, houseCount - 1));
    }

    private static double GetAssetRequestAbilityFactor(
        decimal donorWealth,
        decimal requesterWealth,
        decimal projectedAnnualExpenses,
        int sparePrimaryAssets,
        int spareSecondaryAssets)
    {
        var reserve = GetCashReserve(projectedAnnualExpenses);
        var liquidRatio = (double)Math.Max(0m, donorWealth - reserve)
            / (double)Math.Max(reserve, 1000m);
        var relativeRatio = (double)Math.Max(donorWealth, 0m)
            / (double)Math.Max(requesterWealth + 1000m, 1000m);

        var factor = 0.50
            + Math.Min(0.35, sparePrimaryAssets * 0.18)
            + Math.Min(0.15, spareSecondaryAssets * 0.06)
            + Math.Min(0.15, liquidRatio * 0.08)
            + Math.Min(0.10, Math.Max(0, relativeRatio - 1.0) * 0.05);

        return Math.Clamp(factor, 0.35, 1.25);
    }

    private static bool HasFinancialBreathingRoom(
        decimal wealth,
        decimal projectedAnnualExpenses) =>
        wealth >= GetCashReserve(projectedAnnualExpenses) * 0.75m;

    private static decimal GetCashReserve(
        decimal projectedAnnualExpenses) =>
        Math.Max(
            MinimumCashReserve,
            Math.Max(0m, projectedAnnualExpenses));
}
