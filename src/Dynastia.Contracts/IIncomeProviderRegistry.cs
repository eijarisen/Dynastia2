namespace Dynastia.Contracts;

public interface IIncomeProviderRegistry
{
    IReadOnlyCollection<IIncomeProvider> Providers { get; }

    void Register(
        IIncomeProvider provider);

    decimal GetAnnualIncome(
        IPerson person);
}
