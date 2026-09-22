namespace Dynastia.Contracts;

public interface IJusticeService
{
    void EnsureJustice(
        IPerson person);

    JusticeSnapshot GetStatus(
        IPerson person);

    bool IsImprisoned(
        IPerson person);

    void Imprison(
        IPerson person,
        int sentence,
        string reasonId,
        string reasonName,
        string? reasonDescription = null);

    CourtProtectionSnapshot GetCourtProtection(
        IPerson person);

    int ConvictKnownOffense(
        IPerson person,
        int originalSentence,
        string reasonId,
        string reasonName,
        string? reasonDescription = null,
        decimal baseSentenceMultiplier = 1m);

    decimal GetBailCost(
        IPerson person);

    bool ReleaseFromPrison(
        IPerson person);

    bool HasAttemptedEscapeThisImprisonment(
        IPerson person);

    void MarkEscapeAttempted(
        IPerson person);

    int ExtendSentence(
        IPerson person,
        int years);

    double GetStolenHeirloomSaleDetectionChance(
        IPerson person);
}
