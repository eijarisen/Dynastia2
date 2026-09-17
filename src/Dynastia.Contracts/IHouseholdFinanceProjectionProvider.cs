namespace Dynastia.Contracts;

public interface IHouseholdFinanceProjectionProvider
{
    string Id { get; }

    IReadOnlyList<FinanceBreakdownItem> GetProjectedIncome(
        IPerson householdRepresentative);

    IReadOnlyList<FinanceBreakdownItem> GetProjectedExpenses(
        IPerson householdRepresentative);
}

public interface IHouseholdFinanceProjectionProviderRegistry
{
    IReadOnlyCollection<IHouseholdFinanceProjectionProvider> Providers { get; }

    void Register(IHouseholdFinanceProjectionProvider provider);
}
