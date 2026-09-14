namespace Dynastia.Contracts;

public sealed record JusticeSnapshot(
    bool IsImprisoned,
    int RemainingYears,
    bool IsLifeSentence,
    string? CrimeId,
    string? CrimeName,
    string? CrimeDescription = null);
