using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

internal sealed class LoanThoughtProvider :
    IThoughtProvider
{
    private readonly ILoanService _loans;

    public LoanThoughtProvider(
        ILoanService loans)
    {
        _loans = loans;
    }

    public string Id =>
        "thoughts.loans";

    public IEnumerable<ThoughtCandidate> GetCandidates(
        IPerson person,
        ThoughtContext context)
    {
        if (person.Age < 18)
            yield break;

        var debts =
            _loans.GetDebts(
                person);

        if (debts.Count > 0)
        {
            var household =
                context.Economy.GetHousehold(
                    person);

            if (household?.Wealth < 0)
            {
                yield return new ThoughtCandidate(
                                 "loan.negative_balance",
                                 "economy.debt",
                                 "economy.debt",
                                 82,
                                 ThoughtMoodIds.Concerned,
                                 "🏦",
                                 ThoughtSalienceTraits.None,
                                 "state",
                                 "loan.debt",
                                 "loan.negative_balance"
                             );
            }

            var annualPayment =
                debts.Sum(debt =>
                    debt.AnnualPayment);

            var projectedIncome =
                context.Economy
                    .GetProjectedAnnualIncome(
                        person);

            if (annualPayment >= 500m
                && (projectedIncome <= 0
                    || annualPayment
                        >= projectedIncome * 0.25m))
            {
                yield return new ThoughtCandidate(
                                 "loan.large_payment",
                                 "economy.debt",
                                 "economy.debt",
                                 66,
                                 ThoughtMoodIds.Concerned,
                                 "💸",
                                 ThoughtSalienceTraits.None,
                                 "state",
                                 "loan.payment",
                                 "loan.large_payment"
                             );
            }
        }

        if (_loans.GetLoansGiven(person).Count > 0)
        {
            yield return new ThoughtCandidate(
                             "loan.receivable_income",
                             "economy.investment",
                             "economy.loan_income",
                             38,
                             ThoughtMoodIds.Pleased,
                             "💰",
                             ThoughtSalienceTraits.None,
                             "state",
                             "loan.receivable",
                             "loan.receivable_income"
                         );
        }

        if (context.Events.Any(gameEvent =>
            gameEvent.Type.Equals(
                "loan.repaid",
                StringComparison.OrdinalIgnoreCase)
            && gameEvent.SubjectId == person.Id))
        {
            yield return new ThoughtCandidate(
                             "loan.final_repayment",
                             "economy.debt",
                             "economy.debt.repaid",
                             76,
                             ThoughtMoodIds.Relieved,
                             "🏦",
                             ThoughtSalienceTraits.None,
                             "event",
                             "loan.repaid",
                             "loan.final_repayment"
                         );
        }
    }
}
