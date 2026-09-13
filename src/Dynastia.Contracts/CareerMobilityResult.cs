namespace Dynastia.Contracts;

public sealed record CareerMobilityResult(
    bool Success,
    bool ChangedCareer,
    string? PreviousCareerName,
    string? NewCareerName,
    int PreviousLevel,
    int NewLevel,
    string? Message = null);
