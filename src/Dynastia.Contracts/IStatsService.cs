namespace Dynastia.Contracts;

public interface IStatsService
{
    IReadOnlyList<StatValue> GetStats(IPerson person);
    void EnsureStats(IPerson person);
}
