using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

internal sealed class LoanPaymentYearSystem :
    IYearSystem
{
    private readonly StandardLoanService _loans;
    private readonly IEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly ISuccessionService _succession;
    private readonly IGameEventBus _events;

    public LoanPaymentYearSystem(
        StandardLoanService loans,
        IEconomyService economy,
        IFamilyService family,
        ISuccessionService succession,
        IGameEventBus events)
    {
        _loans = loans;
        _economy = economy;
        _family = family;
        _succession = succession;
        _events = events;
    }

    public string Id =>
        "loans.annual_repayments";

    public YearPhase Phase =>
        YearPhase.Finances;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        ["economy.household_finances"];

    public void Execute(
        IGameState gameState)
    {
        // Materialize every due payment before changing household balances or
        // loan-contract progress. This keeps cross-household settlement
        // independent of the order in which contracts happen to be stored.
        var duePayments = _loans.EnumerateContracts()
            .Where(item => item.Contract.Status == LoanStatus.Active)
            .Select(item => MaterializeDuePayment(
                gameState,
                item.PortfolioOwner,
                item.Contract))
            .Where(item => item is not null)
            .Cast<DuePayment>()
            .ToList();

        // Creditor income is part of this year's Finance phase. Credit all
        // recipients first so equivalent annual resources fund equivalent
        // basic needs regardless of reciprocal loan-contract ordering.
        foreach (var due in duePayments)
        {
            if (due.Contract.IsExternalReceivable
                || due.Contract.CreditorType == LoanCreditorType.Private)
            {
                DistributePrivatePayment(
                    gameState,
                    due.Contract,
                    due.Payment);
            }
        }

        // Simulated borrowers are debited only after all creditor allocations
        // have been realized. External receivables have no simulated borrower.
        foreach (var due in duePayments)
        {
            if (due.BorrowerRepresentative is null)
                continue;

            _economy.ChangeWealthAllowDebt(
                due.BorrowerRepresentative,
                -due.Payment);

            _economy.RecordRealizedExpense(
                due.BorrowerRepresentative,
                "loan repayments",
                due.Payment);
        }

        // Advance each contract exactly once after money movement is complete.
        foreach (var due in duePayments)
        {
            ProgressContract(
                gameState,
                due.PortfolioOwner,
                due.Contract,
                due.Payment);
        }
    }

    private DuePayment? MaterializeDuePayment(
        IGameState gameState,
        IPerson portfolioOwner,
        LoanContractState contract)
    {
        // Principal is transferred during the queued early-action phase. The
        // first scheduled payment is deliberately one Year Advance later.
        if (gameState.Year <= contract.StartYear
            || contract.RemainingAmount <= 0
            || contract.YearsPaid >= contract.DurationYears)
        {
            return null;
        }

        var payment =
            CalculateScheduledPayment(
                contract);

        if (payment <= 0)
            return null;

        if (contract.IsExternalReceivable)
        {
            return new DuePayment(
                portfolioOwner,
                contract,
                payment,
                null);
        }

        var householdId =
            _loans.ResolveServicingHouseholdId(
                portfolioOwner,
                contract);

        if (householdId is not Guid id)
            return null;

        var representative =
            _loans.FindHouseholdRepresentative(
                id);

        if (representative is null)
            return null;

        if (portfolioOwner.Tags.Has("state.dead")
            && !_loans.HasSurvivingFormerPartnerInHousehold(
                portfolioOwner,
                id))
        {
            return null;
        }

        return new DuePayment(
            portfolioOwner,
            contract,
            payment,
            representative);
    }

    private void ProgressContract(
        IGameState gameState,
        IPerson portfolioOwner,
        LoanContractState contract,
        decimal payment)
    {
        contract.RemainingAmount =
            LoanTermsCalculator.RoundCurrency(
                Math.Max(
                    0,
                    contract.RemainingAmount
                    - payment));

        contract.YearsPaid++;

        if (contract.RemainingAmount > 0
            && contract.YearsPaid < contract.DurationYears)
        {
            return;
        }

        contract.RemainingAmount = 0;
        contract.Status = LoanStatus.Completed;

        PublishCompletionEvents(
            gameState,
            portfolioOwner,
            contract);
    }

    private static decimal CalculateScheduledPayment(
        LoanContractState contract)
    {
        var isFinalScheduledPayment =
            contract.YearsPaid + 1
            >= contract.DurationYears;

        var payment =
            isFinalScheduledPayment
                ? contract.RemainingAmount
                : Math.Min(
                    contract.AnnualPayment,
                    contract.RemainingAmount);

        return LoanTermsCalculator.RoundCurrency(
            payment);
    }

    private void DistributePrivatePayment(
        IGameState gameState,
        LoanContractState contract,
        decimal payment)
    {
        var shares =
            contract.CreditorShares
                .Where(share => share.Share > 0)
                .ToList();

        if (shares.Count == 0)
            return;

        decimal allocated = 0;

        for (var index = 0; index < shares.Count; index++)
        {
            var share = shares[index];
            var amount =
                index == shares.Count - 1
                    ? payment - allocated
                    : LoanTermsCalculator.RoundCurrency(
                        payment * share.Share);

            amount =
                LoanTermsCalculator.RoundCurrency(
                    Math.Max(0, amount));

            allocated += amount;

            if (amount <= 0
                || share.OwnerPersonId is not Guid ownerId)
            {
                // An inherited creditor share can leave the dynasty. The
                // external customer still pays the full contract, but that
                // share no longer enters a simulated household.
                continue;
            }

            var owner =
                gameState.People.FirstOrDefault(
                    person => person.Id == ownerId);

            if (owner is null)
                continue;

            if (owner.Tags.Has("state.alive"))
            {
                if (owner.Age < 18)
                {
                    _economy.ChangePendingInheritance(
                        owner,
                        amount);

                    continue;
                }

                var ownerHouseholdId =
                    _economy.GetHouseholdId(
                        owner);

                if (ownerHouseholdId is not null)
                {
                    share.RecipientHouseholdId =
                        ownerHouseholdId;

                    _economy.ApplyAnnualFinanceReceipt(
                        owner,
                        "loan repayments",
                        amount);

                    continue;
                }

                _economy.ChangePendingInheritance(
                    owner,
                    amount);

                continue;
            }

            if (share.RecipientHouseholdId is Guid householdId)
            {
                var representative =
                    _loans.FindHouseholdRepresentative(
                        householdId);

                if (representative is not null
                    && !_economy.IsEstateReady(
                        representative))
                {
                    _economy.ApplyAnnualFinanceReceipt(
                        representative,
                        "loan repayments",
                        amount);
                }
            }
        }
    }

    private void PublishCompletionEvents(
        IGameState gameState,
        IPerson portfolioOwner,
        LoanContractState contract)
    {
        if (!contract.IsExternalReceivable
            && portfolioOwner.Tags.Has("state.alive")
            && _succession.IsControllable(
                portfolioOwner))
        {
            _events.Publish(
                new GameEvent
                {
                    Type = "loan.repaid",
                    Year = gameState.Year,
                    SubjectId = portfolioOwner.Id,
                    Data =
                        new Dictionary<string, string>
                        {
                            ["text"] =
                                contract.CreditorType == LoanCreditorType.Bank
                                    ? $"{_family.GetDisplayName(portfolioOwner)} finished repaying the loan to the " +
                                      $"{_loans.GetExternalCreditorLabel(gameState.Year).ToLowerInvariant()}."
                                    : $"{_family.GetDisplayName(portfolioOwner)} finished repaying a loan."
                        }
                });
        }

        if (contract.CreditorType
            != LoanCreditorType.Private)
        {
            return;
        }

        foreach (var owner in
            contract.CreditorShares
                .Select(share => share.OwnerPersonId)
                .Where(id => id is not null)
                .Distinct()
                .Select(id =>
                    gameState.People.FirstOrDefault(
                        person => person.Id == id))
                .Where(person =>
                    person is not null
                    && person.Tags.Has("state.alive"))
                .Cast<IPerson>())
        {
            var text =
                contract.IsExternalReceivable
                    ? string.IsNullOrWhiteSpace(contract.ExternalBorrowerName)
                        ? $"The outside-customer loan was fully repaid to {_family.GetDisplayName(owner)}."
                        : $"The loan to {contract.ExternalBorrowerName} was fully repaid to {_family.GetDisplayName(owner)}."
                    : $"The private loan was fully repaid to {_family.GetDisplayName(owner)}.";

            _events.Publish(
                new GameEvent
                {
                    Type = "loan.receivable_repaid",
                    Year = gameState.Year,
                    SubjectId = owner.Id,
                    RelatedPersonIds =
                        contract.IsExternalReceivable
                            ? []
                            : [portfolioOwner.Id],
                    Data =
                        new Dictionary<string, string>
                        {
                            ["text"] = text
                        }
                });
        }
    }

    private sealed record DuePayment(
        IPerson PortfolioOwner,
        LoanContractState Contract,
        decimal Payment,
        IPerson? BorrowerRepresentative);
}
