using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

internal sealed class HouseholdFinanceProjectionProviderRegistry :
    IHouseholdFinanceProjectionProviderRegistry
{
    private readonly Dictionary<string, IHouseholdFinanceProjectionProvider>
        _providers = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IHouseholdFinanceProjectionProvider> Providers =>
        _providers.Values.ToList();

    public void Register(IHouseholdFinanceProjectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!_providers.TryAdd(provider.Id, provider))
        {
            throw new InvalidOperationException(
                $"A household finance projection provider with ID '{provider.Id}' is already registered.");
        }
    }
}
