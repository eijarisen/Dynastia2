using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed class ThoughtProviderRegistry :
    IThoughtProviderRegistry
{
    private readonly List<IThoughtProvider>
        _providers = [];

    public IReadOnlyCollection<IThoughtProvider> Providers =>
        _providers;

    public void Register(
        IThoughtProvider provider)
    {
        ArgumentNullException.ThrowIfNull(
            provider);

        if (_providers.Any(
            existing =>
                existing.Id.Equals(
                    provider.Id,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Thought provider '{provider.Id}' is already registered.");
        }

        _providers.Add(
            provider);
    }
}
