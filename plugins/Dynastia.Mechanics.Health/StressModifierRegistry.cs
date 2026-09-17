using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class StressModifierRegistry : IStressModifierRegistry
{
    private readonly List<IStressModifierProvider> _providers = [];

    public void Register(IStressModifierProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (_providers.Any(existing => existing.Id.Equals(provider.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Stress modifier provider '{provider.Id}' is already registered.");
        _providers.Add(provider);
    }

    public IReadOnlyList<StressContribution> GetStressContributions(
        IPerson person,
        int year) =>
        _providers.SelectMany(provider => provider.GetStressContributions(person, year))
            .Where(contribution => contribution.Value > 0)
            .ToList();
}
