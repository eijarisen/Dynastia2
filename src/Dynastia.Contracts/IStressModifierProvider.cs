namespace Dynastia.Contracts;

public interface IStressModifierProvider
{
    string Id { get; }

    IEnumerable<StressContribution> GetStressContributions(
        IPerson person,
        int year);
}
