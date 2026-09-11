using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Dynastia.Mechanics.Thoughts;

internal static class DeterministicThoughtRandom
{
    public static int Index(
        int count,
        params string[] parts)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count));
        }

        var text =
            string.Join(
                "|",
                parts);

        var bytes =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    text));

        var value =
            BinaryPrimitives.ReadUInt64LittleEndian(
                bytes.AsSpan(
                    0,
                    sizeof(ulong)));

        return (int)(
            value
            % (ulong)count);
    }

    public static T Choose<T>(
        IReadOnlyList<T> items,
        params string[] parts)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException(
                "Cannot choose from an empty presentation pool.");
        }

        return items[
            Index(
                items.Count,
                parts)];
    }
}
