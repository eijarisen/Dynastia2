using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

public sealed class LoanContractState
{
    public Guid ContractId { get; set; }
    public LoanCreditorType CreditorType { get; set; }
    public decimal Principal { get; set; }
    public int DurationYears { get; set; }
    public decimal TotalInterestRate { get; set; }
    public decimal AnnualPayment { get; set; }
    public int YearsPaid { get; set; }
    public decimal RemainingAmount { get; set; }
    public int StartYear { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Active;
    public bool IsSelfOriginatedBankLoan { get; set; }
    public bool IsExternalReceivable { get; set; }
    public string? ExternalBorrowerName { get; set; }
    public Guid? ServicingHouseholdId { get; set; }
    public List<LoanCreditorShareState> CreditorShares { get; set; } = [];
}
