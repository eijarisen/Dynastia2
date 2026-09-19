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
        foreach (var item in
            _loans.EnumerateContracts()
                .Where(item =>
                    item.Contract.Status == LoanStatus.Active)
                .ToList())
        {
            ProcessContract(
                gameState,
                item.PortfolioOwner,
                item.Contract);
        }
    }

    private void ProcessContract(
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
            return;
        }

        var payment =
            CalculateScheduledPayment(
                contract);

        if (payment <= 0)
            return;

        if (contract.IsExternalReceivable)
        {
            // The customer exists outside the simulated family economy. No
            // borrower household receives principal or loses repayments.
            DistributePrivatePayment(
                gameState,
                contract,
                payment);
        }
        else
        {
            var householdId =
                _loans.ResolveServicingHouseholdId(
                    portfolioOwner,
                    contract);

            if (householdId is not Guid id)
                return;

            var representative =
                _loans.FindHouseholdRepresentative(
                    id);

            if (representative is null)
                return;

            if (portfolioOwner.Tags.Has("state.dead")
                && !_loans.HasSurvivingFormerPartnerInHousehold(
                    portfolioOwner,
                    id))
            {
                return;
            }

            _economy.ChangeWealthAllowDebt(
                representative,
                -payment);

            _economy.RecordRealizedExpense(
                representative,
                "loan repayments",
                payment);

            if (contract.CreditorType
                == LoanCreditorType.Private)
            {
                DistributePrivatePayment(
                    gameState,
                    contract,
                    payment);
            }
        }

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

                    _economy.ChangeWealth(
                        owner,
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
                    _economy.ChangeWealth(
                        representative,
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
}
