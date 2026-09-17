namespace Dynastia.Mechanics.Economy;

internal static class EconomyAnnualRules
{
    public const decimal NannyExpense = 250m;
    public const decimal EfficientHouseholdMultiplier = 0.95m;

    public static decimal CalculateLivingCosts(
        int memberCount,
        decimal costPerPerson,
        bool hasExceptionalIntellect)
    {
        var amount = memberCount * costPerPerson;

        if (hasExceptionalIntellect)
            amount *= EfficientHouseholdMultiplier;

        return Math.Round(
            amount,
            0,
            MidpointRounding.AwayFromZero);
    }

    public static bool ShouldChargeNanny(
        Dynastia.Contracts.IGameState gameState,
        HouseholdEconomyComponent household)
    {
        if (household.NannyId is not Guid nannyId)
            return false;

        var nanny = gameState.People.FirstOrDefault(person => person.Id == nannyId);
        return nanny is null || !nanny.Tags.Has("role.family_nanny");
    }
}
