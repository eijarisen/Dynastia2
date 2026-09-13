namespace Dynastia.Contracts;

public sealed record LoanTermsInfo(
    decimal Principal,
    int DurationYears,
    decimal TotalInterestRate,
    decimal TotalRepayment,
    decimal AnnualPayment);
