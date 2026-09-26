using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Clicalo.Infrastructure.Tests.Migration;

/// <summary>Builds zips in memory, honest or hostile, for <c>SafeZipReader</c>.</summary>
internal static class Zips
{
    private const uint LocalHeader = 0x04034B50;
    private const uint CentralHeader = 0x02014B50;

    /// <summary>A zip with these entries, in order.</summary>
    public static byte[] Create(
        IEnumerable<(string Name, byte[] Content)> entries,
        CompressionLevel level = CompressionLevel.Optimal
    )
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, level);
                using var stream = entry.Open();
                stream.Write(content);
            }
        }

        return buffer.ToArray();
    }

    public static byte[] Create(params (string Name, string Content)[] entries) =>
        Create(entries.Select(static e => (e.Name, Encoding.UTF8.GetBytes(e.Content))));

    /// <summary><paramref name="length"/> bytes that compress very well.</summary>
    public static byte[] Zeros(int length) => new byte[length];

    /// <summary><paramref name="length"/> hex digits that compress about 2:1.</summary>
    public static byte[] Noise(int length, int seed)
    {
        var random = new Random(seed);
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)"0123456789abcdef"[random.Next(16)];
        }

        return bytes;
    }

    /// <summary>Rewrites the declared uncompressed size of every entry, in the local and the central headers.</summary>
    public static byte[] LieAboutUncompressedSize(byte[] zip, uint declared)
    {
        var copy = (byte[])zip.Clone();
        for (var i = 0; i + 30 <= copy.Length; i++)
        {
            var signature = BinaryPrimitives.ReadUInt32LittleEndian(copy.AsSpan(i));
            if (signature == LocalHeader)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(i + 22), declared);
            }
            else if (signature == CentralHeader && i + 46 <= copy.Length)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(i + 24), declared);
            }
        }

        return copy;
    }

    /// <summary>Rewrites the declared compressed size of every entry, in the local and the central headers.</summary>
    public static byte[] LieAboutCompressedSize(byte[] zip, uint declared)
    {
        var copy = (byte[])zip.Clone();
        for (var i = 0; i + 30 <= copy.Length; i++)
        {
            var signature = BinaryPrimitives.ReadUInt32LittleEndian(copy.AsSpan(i));
            if (signature == LocalHeader)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(i + 18), declared);
            }
            else if (signature == CentralHeader && i + 46 <= copy.Length)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(i + 20), declared);
            }
        }

        return copy;
    }
}
