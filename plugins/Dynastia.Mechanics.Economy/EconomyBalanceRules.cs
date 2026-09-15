namespace Dynastia.Mechanics.Economy;

public static class EconomyBalanceRules
{
    public static decimal ApplyOrdinaryWealthChange(
        decimal startingWealth,
        decimal amount)
    {
        if (amount >= 0)
        {
            // Income and transfers first fill any existing negative balance.
            return RoundCurrency(
                startingWealth + amount);
        }

        if (startingWealth <= 0)
        {
            // Ordinary spending cannot deepen debt and must never erase an
            // existing negative balance by clamping it upward to zero.
            return RoundCurrency(
                startingWealth);
        }

        return RoundCurrency(
            Math.Max(
                0,
                startingWealth + amount));
    }

    public static decimal ApplyOrdinaryAnnualFinance(
        decimal startingWealth,
        decimal income,
        decimal ordinaryExpenses)
    {
        var afterIncome =
            startingWealth
            + income;

        return RoundCurrency(
            afterIncome <= 0
                ? afterIncome
                : Math.Max(
                    0,
                    afterIncome
                    - ordinaryExpenses));
    }

    private static decimal RoundCurrency(
        decimal amount) =>
        Math.Round(
            amount,
            0,
            MidpointRounding.AwayFromZero);
}
