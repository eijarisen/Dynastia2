namespace Dynastia.Contracts;

public interface IStressModifierRegistry
{
    void Register(IStressModifierProvider provider);

    IReadOnlyList<StressContribution> GetStressContributions(
        IPerson person,
        int year);
}
