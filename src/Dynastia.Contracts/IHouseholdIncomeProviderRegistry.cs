namespace Dynastia.Contracts;

public interface IHouseholdIncomeProviderRegistry
{
    IReadOnlyCollection<IHouseholdIncomeProvider> Providers { get; }

    void Register(
        IHouseholdIncomeProvider provider);
}
