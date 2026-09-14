namespace Dynastia.Contracts;

public sealed record LoanContractInfo(
    Guid ContractId,
    string CreditorName,
    decimal RemainingAmount,
    decimal AnnualPayment,
    int YearsRemaining,
    LoanCreditorType CreditorType,
    LoanStatus Status,
    bool IsSelfOriginated,
    string? BorrowerName = null);
