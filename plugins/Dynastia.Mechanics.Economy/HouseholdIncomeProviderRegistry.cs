using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class HouseholdIncomeProviderRegistry :
    IHouseholdIncomeProviderRegistry
{
    private readonly Dictionary<string, IHouseholdIncomeProvider>
        _providers =
            new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IHouseholdIncomeProvider> Providers =>
        _providers.Values.ToList();

    public void Register(
        IHouseholdIncomeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrWhiteSpace(provider.Id))
        {
            throw new InvalidOperationException(
                "Household income provider ID is required.");
        }

        if (!_providers.TryAdd(provider.Id, provider))
        {
            throw new InvalidOperationException(
                $"Household income provider '{provider.Id}' is already registered.");
        }
    }
}
