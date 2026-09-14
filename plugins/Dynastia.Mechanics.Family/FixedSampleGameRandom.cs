using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

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

    public int NextInt(
        int minInclusive,
        int maxInclusive)
    {
        if (minInclusive > maxInclusive)
            throw new ArgumentOutOfRangeException(nameof(minInclusive));

        var range =
            (long)maxInclusive
            - minInclusive
            + 1L;

        return minInclusive
            + (int)Math.Min(
                range - 1,
                Math.Floor(_sample * range));
    }

    public double NextDouble() =>
        _sample;

    public bool Chance(double probability) =>
        _sample < probability;
}
