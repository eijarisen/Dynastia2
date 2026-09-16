namespace Dynastia.Contracts;

public sealed record GeneratedCareerProfile(
    string? CareerId,
    string CareerName,
    string JobTitle,
    int JobLevel,
    int JobSatisfaction,
    decimal AnnualIncome);
