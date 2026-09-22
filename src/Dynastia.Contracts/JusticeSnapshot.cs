namespace Dynastia.Contracts;

public sealed record CriminalRecordEntryInfo(
    int Year,
    string CrimeId,
    string CrimeName,
    int OriginalSentence,
    int FinalSentence);

public sealed record CourtProtectionSnapshot(
    string TierId,
    string DisplayName,
    double Score,
    decimal SentenceMultiplier,
    double StolenSaleDetectionChance,
    Guid? HelperPersonId = null,
    string? HelperName = null,
    string? HelperCareerName = null,
    int HelperJobLevel = 0)
{
    public static CourtProtectionSnapshot None { get; } =
        new(
            "none",
            "None",
            0,
            1m,
            0.30);
}

public sealed record JusticeSnapshot(
    bool IsImprisoned,
    int RemainingYears,
    bool IsLifeSentence,
    string? CrimeId,
    string? CrimeName,
    string? CrimeDescription = null,
    bool EscapeAttemptedThisImprisonment = false,
    IReadOnlyList<CriminalRecordEntryInfo>? CriminalRecord = null)
{
    public IReadOnlyList<CriminalRecordEntryInfo> KnownCriminalRecord =>
        CriminalRecord ?? Array.Empty<CriminalRecordEntryInfo>();
}
