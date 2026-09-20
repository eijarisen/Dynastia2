namespace Dynastia.Contracts;

public enum HouseholdLifestyleStance
{
    Balanced = 0,
    Lavish = 1,
    Thrifty = 2
}

public sealed record HouseholdBudgetHistoryPoint(
    int Year,
    decimal Wealth,
    decimal Income,
    decimal Expenses);

public static class HouseholdLifestyleRules
{
    public const string LavishTag = "household.lifestyle.lavish";
    public const string ThriftyTag = "household.lifestyle.thrifty";

    public static HouseholdLifestyleStance GetStance(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (person.Tags.Has(LavishTag))
            return HouseholdLifestyleStance.Lavish;

        if (person.Tags.Has(ThriftyTag))
            return HouseholdLifestyleStance.Thrifty;

        return HouseholdLifestyleStance.Balanced;
    }

    public static decimal GetLivingCostMultiplier(
        HouseholdLifestyleStance stance) =>
        stance switch
        {
            HouseholdLifestyleStance.Lavish => 1.25m,
            HouseholdLifestyleStance.Thrifty => 0.80m,
            _ => 1.00m
        };

    public static double GetHealthRegenerationModifier(IPerson person) =>
        GetStance(person) switch
        {
            HouseholdLifestyleStance.Lavish => 0.5,
            HouseholdLifestyleStance.Thrifty => -0.5,
            _ => 0.0
        };

    public static double GetEducationChanceMultiplier(IPerson person) =>
        GetStance(person) switch
        {
            HouseholdLifestyleStance.Lavish => 1.08,
            HouseholdLifestyleStance.Thrifty => 0.92,
            _ => 1.00
        };

    public static double GetPartnerChanceMultiplier(IPerson person) =>
        GetStance(person) switch
        {
            HouseholdLifestyleStance.Lavish => 1.10,
            HouseholdLifestyleStance.Thrifty => 0.90,
            _ => 1.00
        };

    public static double GetMarriageSatisfactionModifier(IPerson person) =>
        GetStance(person) switch
        {
            HouseholdLifestyleStance.Lavish => 0.5,
            HouseholdLifestyleStance.Thrifty => -0.5,
            _ => 0.0
        };

    public static double GetStressAdjustment(IPerson person) =>
        GetStance(person) switch
        {
            HouseholdLifestyleStance.Lavish => -0.5,
            HouseholdLifestyleStance.Thrifty => 0.5,
            _ => 0.0
        };

    public static double GetMoraleShiftChance(IPerson person) =>
        GetStance(person) switch
        {
            HouseholdLifestyleStance.Lavish => 0.10,
            HouseholdLifestyleStance.Thrifty => 0.10,
            _ => 0.0
        };
}
