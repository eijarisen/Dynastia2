namespace Dynastia.Mechanics.Stats;

public sealed class StatsComponent
{
    public Dictionary<string, int> Values { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}
