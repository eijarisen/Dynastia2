namespace Dynastia.Contracts;

public sealed record JobOpportunityInfo(
    string CareerId,
    string CareerName,
    string JobTitle,
    int JobLevel,
    decimal AnnualSalary,
    string PrimaryAbility,
    int RequiredAbilityLevel,
    int RequiredEducationLevel,
    int RequiredExperienceYears,
    int ApplicantAbilityLevel,
    int ApplicantEducationLevel,
    int ApplicantExperienceYears,
    double SuccessChance,
    int SearchYear);
