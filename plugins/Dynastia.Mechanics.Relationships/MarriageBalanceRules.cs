namespace Dynastia.Mechanics.Relationships;

public static class MarriageBalanceRules
{
    public const double MiserableThreshold = 20;
    public const double UnhappyThreshold = 40;
    public const double AnnualStability = 4;

    public static double GetAnnualSatisfactionChange(
        double totalPenalty) =>
        AnnualStability - Math.Max(0, totalPenalty);

    public static double GetFinancialPressurePenalty(
        bool isBroke,
        bool hasWorkingSpouse)
    {
        if (!isBroke)
            return 0;

        return hasWorkingSpouse
            ? 2.5
            : 6.0;
    }

    public static double GetAutomaticDivorceChance(
        double satisfaction)
    {
        return satisfaction switch
        {
            < 10 => 0.20,
            < MiserableThreshold => 0.10,
            < 30 => 0.015,
            < UnhappyThreshold => 0.005,
            _ => 0
        };
    }
}
