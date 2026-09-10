using Dynastia.Contracts;

namespace Dynastia.Core.Simulation;

public sealed class GameRandom : IGameRandom
{
    private readonly Random _random;

    public GameRandom(int? seed = null)
    {
        _random = seed.HasValue
            ? new Random(seed.Value)
            : new Random();
    }

    public int NextInt(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxInclusive),
                "Maximum must be greater than or equal to minimum.");
        }

        return _random.Next(minInclusive, maxInclusive + 1);
    }

    public double NextDouble() => _random.NextDouble();

    public bool Chance(double probability)
    {
        if (probability is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probability),
                "Probability must be between 0 and 1.");
        }

        return _random.NextDouble() < probability;
    }
}
