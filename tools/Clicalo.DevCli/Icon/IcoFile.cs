using System.Buffers.Binary;

namespace Clicalo.DevCli.Icon;

/// <summary>
/// Writes and reads a Windows icon file (<c>.ico</c>) with 32-bit images: the ones under 256 pixels as bitmaps with
/// their transparency mask, which every Windows API reads, and the one of 256 as PNG, as Windows itself does.
/// </summary>
internal static class IcoFile
{
    private const int DirectoryHeader = 6;
    private const int DirectoryEntry = 16;
    private const int BitmapHeader = 40;
    private const int PngFrom = 256;

    /// <summary>The file with <paramref name="images"/>, in that order.</summary>
    public static byte[] Write(IReadOnlyList<IcoImage> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        var bodies = images
            .Select(static image => image.Size >= PngFrom ? PngCodec.Encode(image) : Bitmap(image))
            .ToList();
        using var file = new MemoryStream();
        Span<byte> header = stackalloc byte[DirectoryHeader];
        BinaryPrimitives.WriteUInt16LittleEndian(header[2..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..], (ushort)images.Count);
        file.Write(header);

        var offset = DirectoryHeader + (DirectoryEntry * images.Count);
        Span<byte> entry = stackalloc byte[DirectoryEntry];
        for (var i = 0; i < images.Count; i++)
        {
            entry.Clear();

            // A side of 256 is written as 0.
            entry[0] = (byte)(images[i].Size & 0xFF);
            entry[1] = (byte)(images[i].Size & 0xFF);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[4..], 1);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[6..], 32);
            BinaryPrimitives.WriteInt32LittleEndian(entry[8..], bodies[i].Length);
            BinaryPrimitives.WriteInt32LittleEndian(entry[12..], offset);
            file.Write(entry);
            offset += bodies[i].Length;
        }

        foreach (var body in bodies)
        {
            file.Write(body);
        }

        return file.ToArray();
    }

    /// <summary>The images of an icon file written by <see cref="Write"/>.</summary>
    /// <exception cref="InvalidDataException">The file is not one of those.</exception>
    public static IReadOnlyList<IcoImage> Read(ReadOnlySpan<byte> file)
    {
        if (
            file.Length < DirectoryHeader
            || BinaryPrimitives.ReadUInt16LittleEndian(file) != 0
            || BinaryPrimitives.ReadUInt16LittleEndian(file[2..]) != 1
        )
        {
            throw new InvalidDataException("Not an icon file.");
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(file[4..]);
        var images = new List<IcoImage>(count);
        for (var i = 0; i < count; i++)
        {
            var at = DirectoryHeader + (DirectoryEntry * i);
            if (at + DirectoryEntry > file.Length)
            {
                throw new InvalidDataException("The icon directory is cut short.");
            }

            var entry = file.Slice(at, DirectoryEntry);
            var length = BinaryPrimitives.ReadInt32LittleEndian(entry[8..]);
            var offset = BinaryPrimitives.ReadInt32LittleEndian(entry[12..]);
            if (length < 0 || offset < 0 || (long)offset + length > file.Length)
            {
                throw new InvalidDataException("An icon image is cut short.");
            }

            var body = file.Slice(offset, length);
            images.Add(PngCodec.IsPng(body) ? PngCodec.Decode(body) : FromBitmap(body));
        }

        return images;
    }

    private static int MaskStride(int size) => (size + 31) / 32 * 4;

    private static byte[] Bitmap(IcoImage image)
    {
        var size = image.Size;
        var maskStride = MaskStride(size);
        var body = new byte[BitmapHeader + (size * size * 4) + (maskStride * size)];
        var span = body.AsSpan();
        BinaryPrimitives.WriteInt32LittleEndian(span, BitmapHeader);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], size);

        // The height counts the color rows and the mask rows.
        BinaryPrimitives.WriteInt32LittleEndian(span[8..], size * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[14..], 32);
        BinaryPrimitives.WriteInt32LittleEndian(
            span[20..],
            (size * size * 4) + (maskStride * size)
        );

        var mask = BitmapHeader + (size * size * 4);
        for (var row = 0; row < size; row++)
        {
            // Bitmaps store the bottom row first.
            var source = (size - 1 - row) * size * 4;
            image.Bgra.AsSpan(source, size * 4).CopyTo(span[(BitmapHeader + (row * size * 4))..]);
            for (var column = 0; column < size; column++)
            {
                if (image.Bgra[source + (column * 4) + 3] == 0)
                {
                    body[mask + (row * maskStride) + (column / 8)] |= (byte)(0x80 >> (column % 8));
                }
            }
        }

        return body;
    }

    private static IcoImage FromBitmap(ReadOnlySpan<byte> body)
    {
        if (
            body.Length < BitmapHeader
            || BinaryPrimitives.ReadInt32LittleEndian(body) != BitmapHeader
            || BinaryPrimitives.ReadUInt16LittleEndian(body[14..]) != 32
        )
        {
            throw new InvalidDataException("Only 32-bit icon bitmaps are read.");
        }

        var size = BinaryPrimitives.ReadInt32LittleEndian(body[4..]);
        if (
            size is < 1 or > 256
            || BinaryPrimitives.ReadInt32LittleEndian(body[8..]) != size * 2
            || body.Length < BitmapHeader + (size * size * 4)
        )
        {
            throw new InvalidDataException("An icon bitmap has the wrong size.");
        }

        var pixels = new byte[size * size * 4];
        for (var row = 0; row < size; row++)
        {
            body.Slice(BitmapHeader + (row * size * 4), size * 4)
                .CopyTo(pixels.AsSpan((size - 1 - row) * size * 4));
        }

        return new IcoImage(size, pixels);
    }
}
