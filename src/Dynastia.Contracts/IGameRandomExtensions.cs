namespace Dynastia.Contracts;

public static class GameRandomExtensions
{
    public static Guid NextGuid(this IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        Span<byte> bytes = stackalloc byte[16];
        for (var offset = 0; offset < bytes.Length; offset += 4)
        {
            var high = random.NextInt(0, 0xFFFF);
            var low = random.NextInt(0, 0xFFFF);
            var value = (high << 16) | low;
            BitConverter.TryWriteBytes(bytes[offset..], value);
        }

        // RFC 4122 variant/version bits make generated identifiers ordinary
        // UUIDv4 values while remaining deterministic for a deterministic RNG.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
