namespace Dynastia.Contracts;

public sealed record CraftEducationOption(
    string CraftId,
    string CraftName,
    bool IsKnownCraft,
    int CurrentMasteryLevel,
    string CurrentMasteryName,
    double MasteryProgress,
    int RelevantExperienceYears,
    string PrimaryStat,
    int PrimaryStatValue,
    double SuccessChance);

public sealed record CraftEducationResult(
    bool Attempted,
    bool Success,
    bool LearnedNewCraft,
    string CraftId,
    string CraftName,
    int PreviousMasteryLevel,
    int CurrentMasteryLevel,
    double MasteryProgress,
    int RelevantExperienceYears,
    double SuccessChance,
    string? Message = null);
