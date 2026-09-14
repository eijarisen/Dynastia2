using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

public sealed partial class StandardLoanService :
    ILoanService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly LoanEraCatalog _loanEras;

    public StandardLoanService(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        LoanEraCatalog loanEras)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
        _loanEras = loanEras;
    }

    public LoanTermsInfo CalculateTerms(
        decimal principal,
        int durationYears) =>
        LoanTermsCalculator.Calculate(
            principal,
            durationYears);

    public bool HasActiveSelfOriginatedBankLoan(
        IPerson borrower) =>
        GetPortfolio(
            borrower,
            create: false)?
            .Contracts
            .Any(contract =>
                contract.Status != LoanStatus.Completed
                && !contract.IsExternalReceivable
                && contract.CreditorType == LoanCreditorType.Bank
                && contract.IsSelfOriginatedBankLoan)
        == true;

    public IReadOnlyList<LoanContractInfo> GetDebts(
        IPerson householdRepresentative)
    {
        var householdId =
            _economy.GetHouseholdId(
                householdRepresentative);

        if (householdId is null)
            return [];

        return EnumerateContracts()
            .Where(item =>
                item.Contract.Status == LoanStatus.Active
                && !item.Contract.IsExternalReceivable
                && ResolveServicingHouseholdId(
                    item.PortfolioOwner,
                    item.Contract)
                    == householdId)
            .Select(item =>
                ToInfo(
                    item.Contract,
                    1m))
            .OrderBy(info =>
                info.CreditorName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<LoanContractInfo> GetLoansGiven(
        IPerson householdRepresentative)
    {
        var householdId =
            _economy.GetHouseholdId(
                householdRepresentative);

        if (householdId is null)
            return [];

        var result =
            new List<LoanContractInfo>();

        foreach (var item in
            EnumerateContracts()
                .Where(item =>
                    item.Contract.Status == LoanStatus.Active
                    && item.Contract.CreditorType == LoanCreditorType.Private))
        {
            var share =
                item.Contract.CreditorShares
                    .Where(creditorShare =>
                        ResolveCreditorHouseholdId(
                            creditorShare)
                            == householdId)
                    .Sum(creditorShare =>
                        creditorShare.Share);

            if (share <= 0)
                continue;

            result.Add(
                ToInfo(
                    item.Contract,
                    share));
        }

        return result
            .OrderBy(info =>
                info.YearsRemaining)
            .ThenByDescending(info =>
                info.RemainingAmount)
            .ToList();
    }

    internal LoanContractState CreateBankLoan(
        IPerson borrower,
        decimal principal,
        int durationYears,
        int startYear)
    {
        var terms =
            CalculateTerms(
                principal,
                durationYears);

        var contract =
            new LoanContractState
            {
                CreditorType = LoanCreditorType.Bank,
                Principal = terms.Principal,
                DurationYears = terms.DurationYears,
                TotalInterestRate = terms.TotalInterestRate,
                AnnualPayment = terms.AnnualPayment,
                RemainingAmount = terms.TotalRepayment,
                StartYear = startYear,
                Status = LoanStatus.Active,
                IsSelfOriginatedBankLoan = true,
                IsExternalReceivable = false,
                ServicingHouseholdId = _economy.GetHouseholdId(borrower)
            };

        GetPortfolio(borrower, create: true)!
            .Contracts.Add(contract);

        return contract;
    }

    internal LoanContractState CreateExternalReceivable(
        IPerson lender,
        decimal principal,
        int durationYears,
        int startYear)
    {
        var terms =
            CalculateTerms(
                principal,
                durationYears);

        var contract =
            new LoanContractState
            {
                CreditorType = LoanCreditorType.Private,
                Principal = terms.Principal,
                DurationYears = terms.DurationYears,
                TotalInterestRate = terms.TotalInterestRate,
                AnnualPayment = terms.AnnualPayment,
                RemainingAmount = terms.TotalRepayment,
                StartYear = startYear,
                Status = LoanStatus.Active,
                IsSelfOriginatedBankLoan = false,
                IsExternalReceivable = true,
                CreditorShares =
                [
                    new LoanCreditorShareState
                    {
                        OwnerPersonId = lender.Id,
                        RecipientHouseholdId = _economy.GetHouseholdId(lender),
                        Share = 1m
                    }
                ]
            };

        // External customers are intentionally not simulated people or
        // households. The receivable lives in the lender's portfolio and
        // produces deterministic repayments from outside the dynasty.
        GetPortfolio(lender, create: true)!
            .Contracts.Add(contract);

        return contract;
    }

    internal IEnumerable<(IPerson PortfolioOwner, LoanContractState Contract)> EnumerateContracts()
    {
        foreach (var owner in _gameState.People)
        {
            var portfolio =
                GetPortfolio(
                    owner,
                    create: false);

            if (portfolio is null)
                continue;

            foreach (var contract in portfolio.Contracts)
            {
                yield return (
                    owner,
                    contract);
            }
        }
    }

    internal LoanPortfolioComponent? GetPortfolio(
        IPerson person,
        bool create)
    {
        var component =
            person.Components.Get<LoanPortfolioComponent>();

        if (component is not null
            || !create)
        {
            return component;
        }

        component =
            new LoanPortfolioComponent();

        person.Components.Set(
            component);

        return component;
    }

    internal Guid? ResolveServicingHouseholdId(
        IPerson borrower,
        LoanContractState contract)
    {
        if (contract.IsExternalReceivable)
            return null;

        if (borrower.Tags.Has("state.alive"))
        {
            var current =
                _economy.GetHouseholdId(
                    borrower);

            if (current is not null)
            {
                contract.ServicingHouseholdId =
                    current;
            }
        }

        return contract.ServicingHouseholdId;
    }

    internal IPerson? FindHouseholdRepresentative(
        Guid householdId)
    {
        foreach (var person in _gameState.People)
        {
            if (!_economy.HasHousehold(person))
                continue;

            if (_economy.GetHouseholdId(person)
                == householdId)
            {
                return person;
            }
        }

        return null;
    }

    internal Guid? ResolveCreditorHouseholdId(
        LoanCreditorShareState share)
    {
        if (share.OwnerPersonId is Guid ownerId)
        {
            var owner =
                _gameState.People.FirstOrDefault(
                    person => person.Id == ownerId);

            if (owner is not null
                && owner.Tags.Has("state.alive"))
            {
                if (owner.Age < 18)
                    return null;

                var current =
                    _economy.GetHouseholdId(
                        owner);

                if (current is not null)
                {
                    share.RecipientHouseholdId =
                        current;

                    return current;
                }
            }
        }

        return share.RecipientHouseholdId;
    }

    internal bool HasSurvivingFormerPartnerInHousehold(
        IPerson deceasedBorrower,
        Guid householdId)
    {
        foreach (var relationship in
            _family.GetRelationshipHistory(
                deceasedBorrower))
        {
            if (!string.Equals(
                    relationship.EndReason,
                    "death",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var formerPartner =
                _gameState.People.FirstOrDefault(
                    person =>
                        person.Id == relationship.SpouseId);

            if (formerPartner is null
                || !formerPartner.Tags.Has("state.alive"))
            {
                continue;
            }

            if (_economy.GetHouseholdId(
                    formerPartner)
                == householdId)
            {
                return true;
            }
        }

        return false;
    }

    internal string GetExternalCreditorLabel(int year) =>
        _loanEras.GetRule(year).ExternalCreditorLabel;

    internal string GetCreditorName(
        LoanContractState contract)
    {
        if (contract.CreditorType
            == LoanCreditorType.Bank)
        {
            return GetExternalCreditorLabel(
                _gameState.Year);
        }

        var owners =
            contract.CreditorShares
                .Select(share => share.OwnerPersonId)
                .Where(id => id is not null)
                .Distinct()
                .Select(id =>
                    _gameState.People.FirstOrDefault(
                        person => person.Id == id))
                .Where(person => person is not null)
                .Cast<IPerson>()
                .ToList();

        return owners.Count switch
        {
            0 => "Private creditor",
            1 => _family.GetDisplayName(owners[0]),
            _ => "Family heirs"
        };
    }

    private LoanContractInfo ToInfo(
        LoanContractState contract,
        decimal share)
    {
        var remainingYears =
            Math.Max(
                0,
                contract.DurationYears
                - contract.YearsPaid);

        return new LoanContractInfo(
            contract.ContractId,
            GetCreditorName(contract),
            LoanTermsCalculator.RoundCurrency(
                contract.RemainingAmount
                * share),
            LoanTermsCalculator.RoundCurrency(
                contract.AnnualPayment
                * share),
            remainingYears,
            contract.CreditorType,
            contract.Status,
            contract.IsSelfOriginatedBankLoan,
            contract.IsExternalReceivable
                ? contract.ExternalBorrowerName
                : null);
    }
}
