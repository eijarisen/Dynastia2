namespace Dynastia.Contracts;

public interface IAnnualHealthModifierRegistry
{
    IReadOnlyCollection<IAnnualHealthModifierProvider> Providers { get; }
    void Register(IAnnualHealthModifierProvider provider);
    double GetAnnualHealthChange(IPerson person);
}
