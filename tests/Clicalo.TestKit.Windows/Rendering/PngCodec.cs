using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clicalo.TestKit.Windows.Rendering;

/// <summary>PNG encoding and decoding through WIC. Every call runs on <see cref="WpfThread"/>.</summary>
public static class PngCodec
{
    private const double StandardDpi = 96;

    /// <summary>Encodes <paramref name="bitmap"/> (any pixel format) as PNG.</summary>
    public static byte[] Encode(BitmapSource bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return WpfThread.Invoke(() =>
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        });
    }

    /// <summary>Encodes a <see cref="PixelBuffer"/> as a 32-bit PNG with alpha.</summary>
    public static byte[] Encode(PixelBuffer pixels, double dpi = StandardDpi)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        return WpfThread.Invoke(() =>
        {
            var bitmap = BitmapSource.Create(
                pixels.Width,
                pixels.Height,
                dpi,
                dpi,
                PixelFormats.Bgra32,
                palette: null,
                pixels.Bgra,
                pixels.Stride
            );
            bitmap.Freeze();
            return Encode(bitmap);
        });
    }

    /// <summary>Decodes a PNG into straight-alpha BGRA pixels, whatever its stored format.</summary>
    public static PixelBuffer Decode(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        return WpfThread.Invoke(() =>
        {
            using var stream = new MemoryStream(png, writable: false);
            var decoder = new PngBitmapDecoder(
                stream,
                BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad
            );
            var frame = decoder.Frames[0];
            var converted = new FormatConvertedBitmap(
                frame,
                PixelFormats.Bgra32,
                destinationPalette: null,
                alphaThreshold: 0
            );
            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            var bgra = new byte[width * height * PixelBuffer.BytesPerPixel];
            converted.CopyPixels(bgra, width * PixelBuffer.BytesPerPixel, 0);
            return new PixelBuffer(width, height, bgra);
        });
    }
}
