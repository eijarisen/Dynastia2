using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

internal sealed class LoanFinanceProjectionProvider :
    IHouseholdFinanceProjectionProvider
{
    private readonly ILoanService _loans;

    public LoanFinanceProjectionProvider(ILoanService loans)
    {
        _loans = loans;
    }

    public string Id => "loans.annual_projection";

    public IReadOnlyList<FinanceBreakdownItem> GetProjectedIncome(
        IPerson householdRepresentative)
    {
        var amount = _loans
            .GetLoansGiven(householdRepresentative)
            .Sum(loan => loan.AnnualPayment);

        return amount > 0
            ? [new FinanceBreakdownItem("loan repayments", amount)]
            : Array.Empty<FinanceBreakdownItem>();
    }

    public decimal GetProjectedPassiveIncome(
        IPerson householdRepresentative) =>
        _loans.GetLoansGiven(householdRepresentative)
            .Sum(loan => loan.AnnualPayment);

    public IReadOnlyList<FinanceBreakdownItem> GetProjectedExpenses(
        IPerson householdRepresentative)
    {
        var amount = _loans
            .GetDebts(householdRepresentative)
            .Sum(loan => loan.AnnualPayment);

        return amount > 0
            ? [new FinanceBreakdownItem("loan repayments", amount)]
            : Array.Empty<FinanceBreakdownItem>();
    }
}
