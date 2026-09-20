namespace Dynastia.Mechanics.FamilyRelations;

public static class WeddingSupportRules
{
    private const decimal MinimumReserve = 2000m;
    private const decimal MaximumGift = 2500m;

    public static decimal CalculateGift(
        decimal donorWealth,
        decimal projectedAnnualExpenses,
        double familiarity,
        double sympathy)
    {
        if (!FamilySupportAbilityRules.HasStrongCareerConnectionRelation(
                familiarity,
                sympathy))
        {
            return 0m;
        }

        var reserve = Math.Max(
            MinimumReserve,
            Math.Max(0m, projectedAnnualExpenses));
        var disposable = Math.Max(0m, donorWealth - reserve);
        if (disposable <= 0m)
            return 0m;

        var warmStrength = Math.Clamp((sympathy - 60.0) / 40.0, 0, 1);
        var closeStrength = Math.Clamp((familiarity - 75.0) / 25.0, 0, 1);
        var relationStrength = Math.Max(warmStrength, closeStrength);

        var rate = 0.0125m + 0.0125m * (decimal)relationStrength;
        var rawGift = Math.Min(MaximumGift, disposable * rate);
        var rounded = Math.Floor(rawGift / 100m) * 100m;

        if (rounded < 100m)
            return 0m;

        return Math.Min(rounded, disposable);
    }
}
