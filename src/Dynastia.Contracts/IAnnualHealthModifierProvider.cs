namespace Dynastia.Contracts;

public interface IAnnualHealthModifierProvider
{
    string Id { get; }
    double GetAnnualHealthChange(IPerson person);
}
