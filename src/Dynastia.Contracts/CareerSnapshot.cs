namespace Dynastia.Contracts;

public sealed record CareerSnapshot(
    int JobLevel,
    string JobTitle,
    int JobSatisfaction,
    string JobSatisfactionText,
    decimal LastIncome,
    decimal AnnualIncome,
    bool IsRetired);
