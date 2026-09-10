namespace Dynastia.Contracts;

public interface IStatsService
{
    IReadOnlyList<StatValue> GetStats(IPerson person);

    void EnsureStats(IPerson person);

    void SetStats(
        IPerson person,
        IReadOnlyDictionary<string, int> values);
}
