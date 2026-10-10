using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>
/// One image of an icon file (<c>.ico</c>): where its bytes are and how many pixels its side has. Pure: it only reads
/// the directory at the start of the file.
/// </summary>
/// <param name="Offset">Where the image starts in the file.</param>
/// <param name="Length">How many bytes it takes.</param>
/// <param name="Size">Its side, in pixels (16 to 256).</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct IconFileEntry(int Offset, int Length, int Size)
{
    private const int DirectoryHeader = 6;
    private const int DirectoryEntry = 16;
    private const int LargestSide = 256;

    /// <summary>
    /// The image of <paramref name="file"/> to draw at <paramref name="wanted"/> pixels: the smallest one that is at
    /// least that large, so it is never stretched, or the largest one when all are smaller. <see langword="null"/>
    /// when the bytes are not an icon file.
    /// </summary>
    /// <param name="file">The whole icon file.</param>
    /// <param name="wanted">The side the notification area draws, in pixels.</param>
    public static IconFileEntry? Best(ReadOnlySpan<byte> file, int wanted)
    {
        if (
            file.Length < DirectoryHeader
            || BinaryPrimitives.ReadUInt16LittleEndian(file) != 0
            || BinaryPrimitives.ReadUInt16LittleEndian(file[2..]) != 1
        )
        {
            return null;
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(file[4..]);
        IconFileEntry? best = null;
        for (var i = 0; i < count; i++)
        {
            var at = DirectoryHeader + (DirectoryEntry * i);
            if (at + DirectoryEntry > file.Length)
            {
                return null;
            }

            // A side of 256 is written as 0.
            var size = file[at] == 0 ? LargestSide : file[at];
            var length = BinaryPrimitives.ReadInt32LittleEndian(file[(at + 8)..]);
            var offset = BinaryPrimitives.ReadInt32LittleEndian(file[(at + 12)..]);
            if (length <= 0 || offset < 0 || (long)offset + length > file.Length)
            {
                return null;
            }

            var entry = new IconFileEntry(offset, length, size);
            if (best is not { } chosen || IsBetter(entry.Size, chosen.Size, wanted))
            {
                best = entry;
            }
        }

        return best;
    }

    private static bool IsBetter(int candidate, int chosen, int wanted) =>
        chosen >= wanted ? candidate >= wanted && candidate < chosen : candidate > chosen;
}
