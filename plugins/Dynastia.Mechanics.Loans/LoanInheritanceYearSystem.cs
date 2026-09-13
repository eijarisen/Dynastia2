using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

internal sealed class LoanInheritanceYearSystem :
    IYearSystem
{
    private readonly StandardLoanService _loans;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IGameEventBus _events;

    public LoanInheritanceYearSystem(
        StandardLoanService loans,
        IFamilyService family,
        IEconomyService economy,
        IGameEventBus events)
    {
        _loans = loans;
        _family = family;
        _economy = economy;
        _events = events;
    }

    public string Id =>
        "loans.inheritance";

    public YearPhase Phase =>
        YearPhase.Inheritance;

    public IReadOnlyCollection<string> Before =>
        ["inheritance.estate_settlement"];

    public IReadOnlyCollection<string> After =>
        ["households.inheritance_reconcile"];

    public void Execute(
        IGameState gameState)
    {
        ActivateAdultInheritedDebts(
            gameState);

        foreach (var item in
            _loans.EnumerateContracts()
                .Where(item =>
                    item.Contract.Status != LoanStatus.Completed)
                .ToList())
        {
            if (item.Contract.CreditorType
                == LoanCreditorType.Private)
            {
                ReconcileCreditorInheritance(
                    gameState,
                    item.Contract);
            }
        }

        foreach (var item in
            _loans.EnumerateContracts()
                .Where(item =>
                    item.Contract.Status == LoanStatus.Active
                    && !item.Contract.IsExternalReceivable
                    && item.PortfolioOwner.Tags.Has("state.dead"))
                .ToList())
        {
            ReconcileBorrowerDeath(
                gameState,
                item.PortfolioOwner,
                item.Contract);
        }
    }

    private void ActivateAdultInheritedDebts(
        IGameState gameState)
    {
        foreach (var item in
            _loans.EnumerateContracts()
                .Where(item =>
                    item.Contract.Status == LoanStatus.PendingInherited
                    && !item.Contract.IsExternalReceivable)
                .ToList())
        {
            var householdId =
                _economy.GetHouseholdId(
                    item.PortfolioOwner);

            if (!item.PortfolioOwner.Tags.Has("state.alive")
                || item.PortfolioOwner.Age < 18
                || householdId is null)
            {
                continue;
            }

            item.Contract.Status =
                LoanStatus.Active;

            item.Contract.ServicingHouseholdId =
                householdId;

            // Inheritance runs after the finance phase. Starting the clock in
            // the present year therefore makes the first payment due on the
            // next Year Advance, the first finance pass of the new household.
            item.Contract.StartYear =
                gameState.Year;
        }
    }

    private void ReconcileBorrowerDeath(
        IGameState gameState,
        IPerson borrower,
        LoanContractState contract)
    {
        if (contract.ServicingHouseholdId is Guid householdId)
        {
            var representative =
                _loans.FindHouseholdRepresentative(
                    householdId);

            // A surviving spouse/other living household head continues the
            // original debt until that deferred estate is actually settled.
            if (representative is not null
                && _loans.HasSurvivingFormerPartnerInHousehold(
                    borrower,
                    householdId)
                && !_economy.IsEstateReady(
                    representative))
            {
                return;
            }
        }

        var heirs =
            _family.GetChildren(
                borrower)
            .Where(child =>
                child.Tags.Has("state.alive"))
            .OrderBy(child =>
                child.BirthDate?.Year
                ?? int.MaxValue)
            .ThenBy(child =>
                child.BirthDate?.Month
                ?? 1)
            .ThenBy(child =>
                child.BirthDate?.Day
                ?? 1)
            .ThenBy(child =>
                child.Id)
            .ToList();

        if (heirs.Count == 0)
        {
            contract.Status =
                LoanStatus.Completed;

            contract.RemainingAmount = 0;

            return;
        }

        var remainingYears =
            Math.Max(
                1,
                contract.DurationYears
                - contract.YearsPaid);

        var balances =
            SplitCurrency(
                contract.RemainingAmount,
                heirs.Count);

        var annualPayments =
            SplitCurrency(
                contract.AnnualPayment,
                heirs.Count);

        var principals =
            SplitCurrency(
                contract.Principal,
                heirs.Count);

        for (var index = 0; index < heirs.Count; index++)
        {
            var heir = heirs[index];
            var portfolio =
                _loans.GetPortfolio(
                    heir,
                    create: true)!;

            var heirHouseholdId =
                heir.Age >= 18
                    ? _economy.GetHouseholdId(heir)
                    : null;

            var hasAdultHousehold =
                heirHouseholdId is not null;

            portfolio.Contracts.Add(
                new LoanContractState
                {
                    CreditorType = contract.CreditorType,
                    Principal = principals[index],
                    DurationYears = remainingYears,
                    TotalInterestRate = contract.TotalInterestRate,
                    AnnualPayment = annualPayments[index],
                    YearsPaid = 0,
                    RemainingAmount = balances[index],
                    StartYear = gameState.Year,
                    Status = hasAdultHousehold
                        ? LoanStatus.Active
                        : LoanStatus.PendingInherited,
                    IsSelfOriginatedBankLoan = false,
                    IsExternalReceivable = false,
                    ServicingHouseholdId =
                        heirHouseholdId,
                    CreditorShares =
                        contract.CreditorShares
                            .Select(share =>
                                new LoanCreditorShareState
                                {
                                    OwnerPersonId = share.OwnerPersonId,
                                    RecipientHouseholdId = share.RecipientHouseholdId,
                                    Share = share.Share
                                })
                            .ToList()
                });
        }

        contract.Status =
            LoanStatus.Completed;

        contract.RemainingAmount = 0;

        _events.Publish(
            new GameEvent
            {
                Type = "loan.debt_inherited",
                Year = gameState.Year,
                SubjectId = borrower.Id,
                RelatedPersonIds =
                    heirs.Select(heir => heir.Id).ToList(),
                Data =
                    new Dictionary<string, string>
                    {
                        ["text"] =
                            $"The remaining debt of {_family.GetDisplayName(borrower)} was divided among the living children."
                    }
            });
    }

    private void ReconcileCreditorInheritance(
        IGameState gameState,
        LoanContractState contract)
    {
        var replacement =
            new List<LoanCreditorShareState>();

        var changed = false;

        foreach (var share in
            contract.CreditorShares)
        {
            if (share.OwnerPersonId is not Guid ownerId)
            {
                replacement.Add(share);
                continue;
            }

            var owner =
                gameState.People.FirstOrDefault(
                    person => person.Id == ownerId);

            if (owner is null
                || owner.Tags.Has("state.alive"))
            {
                replacement.Add(share);
                continue;
            }

            if (share.RecipientHouseholdId is Guid householdId)
            {
                var representative =
                    _loans.FindHouseholdRepresentative(
                        householdId);

                if (representative is not null
                    && representative.Tags.Has("state.alive")
                    && !_economy.IsEstateReady(
                        representative))
                {
                    replacement.Add(share);
                    continue;
                }
            }

            var heirs =
                _family.GetChildren(owner)
                    .Where(child =>
                        child.Tags.Has("state.alive"))
                    .OrderBy(child =>
                        child.BirthDate?.Year
                        ?? int.MaxValue)
                    .ThenBy(child =>
                        child.BirthDate?.Month
                        ?? 1)
                    .ThenBy(child =>
                        child.BirthDate?.Day
                        ?? 1)
                    .ThenBy(child =>
                        child.Id)
                    .ToList();

            changed = true;

            if (heirs.Count == 0)
            {
                // The receivable left the dynasty. Keep the share so the
                // borrower still pays the full contractual amount, but make
                // its future recipient external.
                replacement.Add(
                    new LoanCreditorShareState
                    {
                        Share = share.Share
                    });

                continue;
            }

            var perHeir =
                share.Share
                / heirs.Count;

            decimal assigned = 0;

            for (var index = 0; index < heirs.Count; index++)
            {
                var heir = heirs[index];
                var heirShare =
                    index == heirs.Count - 1
                        ? share.Share - assigned
                        : perHeir;

                assigned += heirShare;

                replacement.Add(
                    new LoanCreditorShareState
                    {
                        OwnerPersonId = heir.Id,
                        RecipientHouseholdId =
                            heir.Age >= 18
                                ? _economy.GetHouseholdId(heir)
                                : null,
                        Share = heirShare
                    });
            }
        }

        if (changed)
        {
            contract.CreditorShares =
                replacement;
        }
    }

    private static IReadOnlyList<decimal> SplitCurrency(
        decimal amount,
        int count)
    {
        var result =
            new decimal[count];

        if (count <= 0)
            return result;

        var totalZloty =
            decimal.ToInt64(
                Math.Round(
                    amount,
                    0,
                    MidpointRounding.AwayFromZero));

        var baseZloty =
            totalZloty / count;

        var remainder =
            totalZloty % count;

        for (var index = 0; index < count; index++)
        {
            result[index] =
                baseZloty
                + (index < remainder ? 1 : 0);
        }

        return result;
    }
}
