namespace Clicalo.TestKit.Windows.Rendering;

/// <summary>An image as 32-bit BGRA pixels with straight (not premultiplied) alpha, rows top to bottom, no padding.</summary>
public sealed class PixelBuffer
{
    /// <summary>Creates a buffer; <paramref name="bgra"/> must hold exactly <c>width × height × 4</c> bytes.</summary>
    public PixelBuffer(int width, int height, byte[] bgra)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(bgra);
        if (bgra.LongLength != (long)width * height * BytesPerPixel)
        {
            throw new ArgumentException("The pixel data does not match the size.", nameof(bgra));
        }

        Width = width;
        Height = height;
        Bgra = bgra;
    }

    /// <summary>Bytes per pixel (B, G, R, A).</summary>
    public const int BytesPerPixel = 4;

    /// <summary>Width in pixels.</summary>
    public int Width { get; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; }

    /// <summary>Stride in bytes.</summary>
    public int Stride => Width * BytesPerPixel;

    /// <summary>The pixel data (shared, not copied).</summary>
    public ReadOnlyMemory<byte> Pixels => Bgra;

    internal byte[] Bgra { get; }

    /// <summary>A buffer filled with one color.</summary>
    public static PixelBuffer Filled(
        int width,
        int height,
        byte blue,
        byte green,
        byte red,
        byte alpha
    )
    {
        var bgra = new byte[width * height * BytesPerPixel];
        for (var i = 0; i < bgra.Length; i += BytesPerPixel)
        {
            bgra[i] = blue;
            bgra[i + 1] = green;
            bgra[i + 2] = red;
            bgra[i + 3] = alpha;
        }

        return new PixelBuffer(width, height, bgra);
    }

    /// <summary>A copy of this buffer, for tests that alter pixels.</summary>
    public PixelBuffer Clone() => new(Width, Height, (byte[])Bgra.Clone());

    /// <summary>Sets one pixel (tests and diff images).</summary>
    public void SetPixel(int x, int y, byte blue, byte green, byte red, byte alpha)
    {
        var offset = Offset(x, y);
        Bgra[offset] = blue;
        Bgra[offset + 1] = green;
        Bgra[offset + 2] = red;
        Bgra[offset + 3] = alpha;
    }

    /// <summary>Byte offset of pixel (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public int Offset(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);
        return (y * Width + x) * BytesPerPixel;
    }
}
