namespace Dynastia.Contracts;

public sealed record CraftProgressSnapshot(
    string CraftId,
    string CraftName,
    int MasteryLevel,
    string MasteryName,
    double MasteryProgress,
    double ExperienceProgress,
    double EducationProgress,
    int RelevantExperienceYears,
    int SelfEmploymentYears,
    int CreditedPreLearningCareerYears,
    decimal ExpectedAnnualIncome,
    double? NextLevelRequiredProgress,
    int? NextLevelMinimumExperienceYears);
