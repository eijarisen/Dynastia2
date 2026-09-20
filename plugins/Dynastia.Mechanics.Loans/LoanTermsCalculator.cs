using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

public static class LoanTermsCalculator
{
    public const int MinimumPrincipal = 1000;
    public const int MaximumPrincipal = 10000;
    public const int PrincipalStep = 1000;
    public const int MinimumDurationYears = 1;
    public const int MaximumDurationYears = 50;

    public static LoanTermsInfo Calculate(
        decimal principal,
        int durationYears,
        decimal interestMultiplier = 1m)
    {
        if (principal < MinimumPrincipal
            || principal > MaximumPrincipal
            || principal % PrincipalStep != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(principal),
                "Loan principal must be 1,000-10,000 zł in 1,000 zł increments.");
        }

        if (durationYears < MinimumDurationYears
            || durationYears > MaximumDurationYears)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationYears),
                "Loan duration must be 1-50 years.");
        }

        if (interestMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interestMultiplier),
                "Loan interest multiplier must be positive.");
        }

        var progress =
            (durationYears - 1m)
            / (MaximumDurationYears - MinimumDurationYears);

        var baseTotalInterestRate =
            0.20m
            + (1.80m * progress);

        var totalInterestRate =
            baseTotalInterestRate
            * interestMultiplier;

        // Loan money is expressed in whole zł. Scheduled payments and the
        // final rounding remainder must therefore never introduce grosze.
        var totalRepayment =
            RoundCurrency(
                principal
                * (1m + totalInterestRate));

        var annualPayment =
            RoundCurrency(
                totalRepayment
                / durationYears);

        return new LoanTermsInfo(
            principal,
            durationYears,
            totalInterestRate,
            totalRepayment,
            annualPayment,
            interestMultiplier);
    }

    internal static decimal RoundCurrency(
        decimal value) =>
        Math.Round(
            value,
            0,
            MidpointRounding.AwayFromZero);
}
