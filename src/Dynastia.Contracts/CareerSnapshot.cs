namespace Dynastia.Contracts;

public sealed record CareerSnapshot(
    int JobLevel,
    string JobTitle,
    int JobSatisfaction,
    string JobSatisfactionText,
    decimal LastIncome,
    decimal AnnualIncome,
    bool IsRetired,
    string? CareerId = null,
    string? CareerName = null,
    decimal BaseSalary = 0,
    int PeakJobLevel = 0,
    string? PeakCareerId = null,
    string? PeakJobTitle = null,
    string? StatusId = null);
