namespace Dynastia.Contracts;

public interface IStatsService
{
    // Effective values used by normal gameplay and UI:
    // base + acquired improvements, capped at 5.
    IReadOnlyList<StatValue> GetStats(
        IPerson person);

    // Hereditary/base values used by genetics.
    IReadOnlyList<StatValue> GetBaseStats(
        IPerson person);

    void EnsureStats(
        IPerson person);

    void SetStats(
        IPerson person,
        IReadOnlyDictionary<string, int> values);

    bool TryIncreaseAcquiredStat(
        IPerson person,
        string statId);
}
