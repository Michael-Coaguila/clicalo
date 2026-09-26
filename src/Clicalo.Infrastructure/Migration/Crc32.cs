namespace Clicalo.Infrastructure.Migration;

/// <summary>
/// The CRC-32 of zip entries (IEEE 802.3, reflected, polynomial <c>0xEDB88320</c>). <see cref="SafeZipReader"/> checks it
/// on every entry it reads, because the decompressor stops at the size the header declares: a header that understates
/// the size would otherwise hand back a silently truncated file (MIG-009).
/// </summary>
internal static class Crc32
{
    private static readonly uint[] Table = CreateTable();

    /// <summary>Adds <paramref name="data"/> to a running CRC; start with 0.</summary>
    /// <param name="crc">The CRC so far.</param>
    /// <param name="data">The next bytes.</param>
    public static uint Append(uint crc, ReadOnlySpan<byte> data)
    {
        var value = ~crc;
        foreach (var b in data)
        {
            value = Table[(value ^ b) & 0xFF] ^ (value >> 8);
        }

        return ~value;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (var i = 0u; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
