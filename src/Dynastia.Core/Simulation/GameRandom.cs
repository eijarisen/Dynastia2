using System.Security.Cryptography;
using Dynastia.Contracts;

namespace Dynastia.Core.Simulation;

public sealed class GameRandom : IStatefulGameRandom
{
    private ulong _state0;
    private ulong _state1;

    public GameRandom(int? seed = null)
    {
        ulong initial;
        if (seed.HasValue)
        {
            initial = unchecked((ulong)(long)seed.Value);
        }
        else
        {
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);
            initial = BitConverter.ToUInt64(bytes);
        }

        Seed(initial);
    }

    public int NextInt(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxInclusive),
                "Maximum must be greater than or equal to minimum.");
        }

        var range =
            (ulong)((long)maxInclusive - minInclusive) + 1UL;

        if (range == 1)
            return minInclusive;

        // Rejection sampling avoids modulo bias without changing callers.
        var limit = ulong.MaxValue - (ulong.MaxValue % range);
        ulong sample;
        do
        {
            sample = NextUInt64();
        }
        while (sample >= limit);

        return (int)(
            (long)minInclusive
            + (long)(sample % range));
    }

    public double NextDouble()
    {
        // 53 random bits mapped to [0, 1), matching the precision expected
        // by the existing probability code.
        return (NextUInt64() >> 11)
            * (1.0 / (1UL << 53));
    }

    public bool Chance(double probability)
    {
        if (probability is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probability),
                "Probability must be between 0 and 1.");
        }

        return NextDouble() < probability;
    }

    public GameRandomState CaptureState() =>
        new(_state0, _state1);

    public void RestoreState(GameRandomState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.State0 == 0 && state.State1 == 0)
        {
            throw new InvalidDataException(
                "The saved random-number state is invalid.");
        }

        _state0 = state.State0;
        _state1 = state.State1;
    }

    private void Seed(ulong seed)
    {
        var value = seed;
        _state0 = SplitMix64(ref value);
        _state1 = SplitMix64(ref value);

        if (_state0 == 0 && _state1 == 0)
            _state1 = 0x9E3779B97F4A7C15UL;
    }

    private ulong NextUInt64()
    {
        // xoroshiro128+; state is only two ulongs, making save/restore exact.
        var s0 = _state0;
        var s1 = _state1;
        var result = s0 + s1;

        s1 ^= s0;
        _state0 = RotateLeft(s0, 55) ^ s1 ^ (s1 << 14);
        _state1 = RotateLeft(s1, 36);

        return result;
    }

    private static ulong RotateLeft(ulong value, int offset) =>
        (value << offset) | (value >> (64 - offset));

    private static ulong SplitMix64(ref ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        var z = value;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
