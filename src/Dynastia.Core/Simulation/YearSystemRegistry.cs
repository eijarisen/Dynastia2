using Dynastia.Contracts;

namespace Dynastia.Core.Simulation;

public sealed class YearSystemRegistry : IYearSystemRegistry
{
    private readonly List<IYearSystem> _systems = [];

    public IReadOnlyCollection<IYearSystem> Systems => _systems;

    public void Register(IYearSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        if (_systems.Any(x =>
            string.Equals(x.Id, system.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"A year system with ID '{system.Id}' is already registered.");
        }

        _systems.Add(system);
    }
}
