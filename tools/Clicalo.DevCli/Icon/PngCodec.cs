using System.Buffers.Binary;
using System.IO.Compression;

namespace Clicalo.DevCli.Icon;

/// <summary>
/// The smallest PNG writer and reader the 256 pixel image of an icon needs: 8 bits per channel with alpha, no
/// interlacing and no row filter. The reader only reads what the writer writes; anything else is refused.
/// </summary>
internal static class PngCodec
{
    private const int HeaderLength = 13;
    private const byte TruecolorAlpha = 6;

    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>Whether <paramref name="data"/> starts like a PNG file.</summary>
    public static bool IsPng(ReadOnlySpan<byte> data) =>
        data.Length >= Signature.Length && data[..Signature.Length].SequenceEqual(Signature);

    /// <summary>Writes <paramref name="image"/> as a PNG file.</summary>
    public static byte[] Encode(IcoImage image)
    {
        var size = image.Size;
        var rows = new byte[size * ((size * 4) + 1)];
        for (var row = 0; row < size; row++)
        {
            // Filter 0 (none) at the start of every row, then red, green, blue and alpha.
            var target = (row * ((size * 4) + 1)) + 1;
            for (var column = 0; column < size; column++)
            {
                var source = ((row * size) + column) * 4;
                rows[target++] = image.Bgra[source + 2];
                rows[target++] = image.Bgra[source + 1];
                rows[target++] = image.Bgra[source];
                rows[target++] = image.Bgra[source + 3];
            }
        }

        using var packed = new MemoryStream();
        using (var zlib = new ZLibStream(packed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(rows);
        }

        var header = new byte[HeaderLength];
        BinaryPrimitives.WriteInt32BigEndian(header, size);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), size);
        header[8] = 8;
        header[9] = TruecolorAlpha;

        using var file = new MemoryStream();
        file.Write(Signature);
        WriteChunk(file, "IHDR"u8, header);
        WriteChunk(file, "IDAT"u8, packed.ToArray());
        WriteChunk(file, "IEND"u8, []);
        return file.ToArray();
    }

    /// <summary>Reads a PNG file written by <see cref="Encode"/>.</summary>
    /// <exception cref="InvalidDataException">The file is not one of those.</exception>
    public static IcoImage Decode(ReadOnlySpan<byte> data)
    {
        if (!IsPng(data))
        {
            throw new InvalidDataException("Not a PNG file.");
        }

        var size = 0;
        using var packed = new MemoryStream();
        var position = Signature.Length;
        while (position + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(data[position..]);
            var type = data.Slice(position + 4, 4);
            if (length < 0 || position + 12 + length > data.Length)
            {
                throw new InvalidDataException("A PNG chunk is cut short.");
            }

            var body = data.Slice(position + 8, length);
            if (type.SequenceEqual("IHDR"u8))
            {
                size = ReadHeader(body);
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                packed.Write(body);
            }

            position += 12 + length;
        }

        if (size == 0)
        {
            throw new InvalidDataException("The PNG file has no header.");
        }

        packed.Position = 0;
        var stride = (size * 4) + 1;
        var rows = new byte[size * stride];
        using (var zlib = new ZLibStream(packed, CompressionMode.Decompress))
        {
            zlib.ReadExactly(rows);
        }

        var pixels = new byte[size * size * 4];
        for (var row = 0; row < size; row++)
        {
            var source = row * stride;
            if (rows[source++] != 0)
            {
                throw new InvalidDataException("Only PNG rows without a filter are read.");
            }

            for (var column = 0; column < size; column++)
            {
                var target = ((row * size) + column) * 4;
                pixels[target + 2] = rows[source++];
                pixels[target + 1] = rows[source++];
                pixels[target] = rows[source++];
                pixels[target + 3] = rows[source++];
            }
        }

        return new IcoImage(size, pixels);
    }

    private static int ReadHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length != HeaderLength)
        {
            throw new InvalidDataException("The PNG header has the wrong length.");
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(header);
        var height = BinaryPrimitives.ReadInt32BigEndian(header[4..]);
        if (
            width != height
            || width is < 1 or > 256
            || header[8] != 8
            || header[9] != TruecolorAlpha
            || header[12] != 0
        )
        {
            throw new InvalidDataException("Only square PNG images of 8 bits with alpha are read.");
        }

        return width;
    }

    private static void WriteChunk(Stream file, ReadOnlySpan<byte> type, ReadOnlySpan<byte> body)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, body.Length);
        file.Write(number);
        file.Write(type);
        file.Write(body);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc(type, body));
        file.Write(number);
    }

    private static uint Crc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> body)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in type)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (var value in body)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
