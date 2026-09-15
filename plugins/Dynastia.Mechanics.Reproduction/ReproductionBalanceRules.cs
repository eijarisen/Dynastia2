namespace Dynastia.Mechanics.Reproduction;

public static class ReproductionBalanceRules
{
    public const double FailedActiveAttemptMarriagePenalty = -2.0;

    public static double GetMarriageSatisfactionChange(
        bool activelyTried,
        bool conceived) =>
        activelyTried && !conceived
            ? FailedActiveAttemptMarriagePenalty
            : 0.0;
}
