namespace Dynastia.Contracts;

public interface ILoanService
{
    LoanTermsInfo CalculateTerms(
        decimal principal,
        int durationYears,
        decimal interestMultiplier = 1m);

    IReadOnlyList<LoanOfferInfo> GetOffers(
        IPerson householdRepresentative,
        bool isGivingLoan,
        decimal maximumPrincipal);

    bool HasActiveSelfOriginatedBankLoan(
        IPerson borrower);

    IReadOnlyList<LoanContractInfo> GetDebts(
        IPerson householdRepresentative);

    IReadOnlyList<LoanContractInfo> GetLoansGiven(
        IPerson householdRepresentative);
}
