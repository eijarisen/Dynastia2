namespace Dynastia.Mechanics.Stats;

public sealed class StatsComponent
{
    // Hereditary/base values. Genetics reads only this dictionary.
    public Dictionary<string, int> Values { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Purchased/acquired improvements. Normal gameplay sees
    // base + acquired, capped at 5.
    public Dictionary<string, int> AcquiredImprovements { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}
