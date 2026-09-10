using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class IncomeProviderRegistry :
    IIncomeProviderRegistry
{
    private readonly Dictionary<
        string,
        IIncomeProvider>
        _providers =
            new(
                StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IIncomeProvider>
        Providers =>
            _providers.Values.ToList();

    public void Register(
        IIncomeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(
            provider);

        if (string.IsNullOrWhiteSpace(
            provider.Id))
        {
            throw new InvalidOperationException(
                "Income provider ID is required.");
        }

        if (!_providers.TryAdd(
            provider.Id,
            provider))
        {
            throw new InvalidOperationException(
                $"Income provider '{provider.Id}' " +
                "is already registered.");
        }
    }

    public decimal GetAnnualIncome(
        IPerson person)
    {
        return _providers.Values.Sum(
            provider =>
                provider.GetAnnualIncome(
                    person));
    }
}
