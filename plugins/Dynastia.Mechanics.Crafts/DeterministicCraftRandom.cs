using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Dynastia.Mechanics.Crafts;

internal sealed class DeterministicCraftRandom
{
    private ulong _state;

    public DeterministicCraftRandom(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        _state = BinaryPrimitives.ReadUInt64LittleEndian(hash.AsSpan(0, sizeof(ulong)));
        if (_state == 0)
            _state = 0x9E3779B97F4A7C15UL;
    }

    public double NextDouble() =>
        (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public int NextInt(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxInclusive));

        var span = (ulong)(maxInclusive - minInclusive + 1);
        return minInclusive + (int)(NextUInt64() % span);
    }

    private ulong NextUInt64()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 2685821657736338717UL;
    }
}
