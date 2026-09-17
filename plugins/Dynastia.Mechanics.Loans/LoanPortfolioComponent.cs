using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

[PersistedComponentId("loans.portfolio")]
public sealed class LoanPortfolioComponent
{
    public List<LoanContractState> Contracts { get; set; } = [];
}
