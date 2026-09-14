using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal sealed class FixedSampleGameRandom : IGameRandom
{
    private readonly double _sample;

    public FixedSampleGameRandom(double sample)
    {
        _sample = Math.Clamp(
            sample,
            0d,
            Math.BitDecrement(1d));
    }

    public int NextInt(int minInclusive, int maxInclusive) =>
        minInclusive;

    public double NextDouble() =>
        _sample;

    public bool Chance(double probability) =>
        _sample < probability;
}
