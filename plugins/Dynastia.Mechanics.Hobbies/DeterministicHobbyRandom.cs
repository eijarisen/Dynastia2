using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Dynastia.Mechanics.Hobbies;

internal static class DeterministicHobbyRandom
{
    public static double Roll(
        params string[] parts)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(
                string.Join("|", parts)));

        var value = BinaryPrimitives.ReadUInt64LittleEndian(
            bytes.AsSpan(0, sizeof(ulong)));

        return value / ((double)ulong.MaxValue + 1.0);
    }

    public static T Choose<T>(
        IReadOnlyList<T> items,
        params string[] parts)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Cannot choose from an empty hobby pool.");

        var index = (int)Math.Floor(Roll(parts) * items.Count);
        return items[Math.Min(index, items.Count - 1)];
    }
}
