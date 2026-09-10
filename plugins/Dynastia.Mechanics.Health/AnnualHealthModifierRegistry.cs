using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class AnnualHealthModifierRegistry : IAnnualHealthModifierRegistry
{
    private readonly Dictionary<string, IAnnualHealthModifierProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IAnnualHealthModifierProvider> Providers =>
        _providers.Values.ToList();

    public void Register(IAnnualHealthModifierProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrWhiteSpace(provider.Id))
            throw new InvalidOperationException("Health modifier provider ID is required.");

        if (!_providers.TryAdd(provider.Id, provider))
        {
            throw new InvalidOperationException(
                $"Health modifier provider '{provider.Id}' is already registered.");
        }
    }

    public double GetAnnualHealthChange(IPerson person) =>
        _providers.Values.Sum(provider => provider.GetAnnualHealthChange(person));
}
