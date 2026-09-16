using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal sealed class DeterministicRelationshipRandom : IGameRandom
{
    private ulong _state;

    public DeterministicRelationshipRandom(string key)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(key));
        _state = BinaryPrimitives.ReadUInt64LittleEndian(
            hash.AsSpan(0, sizeof(ulong)));
        if (_state == 0)
            _state = 0xD1B54A32D192ED03UL;
    }

    public int NextInt(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxInclusive));

        var span = (ulong)(maxInclusive - minInclusive + 1);
        return minInclusive + (int)(NextUInt64() % span);
    }

    public double NextDouble() =>
        (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public bool Chance(double probability) =>
        NextDouble() < Math.Clamp(probability, 0, 1);

    private ulong NextUInt64()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 2685821657736338717UL;
    }
}
